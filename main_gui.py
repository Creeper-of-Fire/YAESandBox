# main_gui.py
import datetime  # 需要导入 datetime
import logging
import re  # 需要导入 re
import sys
from pathlib import Path
from typing import List, Dict, Optional, Any, cast, Literal

from PySide6 import QtGui  # 导入 QtGui
from PySide6.QtCore import Qt, QThread, Signal, Slot, QObject
from PySide6.QtWidgets import (
    QApplication, QMainWindow, QWidget, QHBoxLayout, QVBoxLayout,
    QTextEdit, QPushButton, QLineEdit,
    QMessageBox, QSplitter, QInputDialog  # 导入 QInputDialog
)

import prompts
# --- 导入自定义模块 ---
# 使用 try-except 块增加启动时的健壮性
from ai_service import AIService
from game_state import GameState, load_game  # 导入 load_game
from parser import parse_commands  # 导入 DiceRollRequest

# --- 配置日志 (依然输出到控制台，Debug 窗口是下一步) ---
logging.basicConfig(level=logging.INFO, format='%(asctime)s - %(levelname)s - %(message)s')

# --- 指令排序函数 ---
def sort_commands_for_execution(commands: List[Dict[str, Any]]) -> List[Dict[str, Any]]:
    """
    对解析出的指令列表进行排序，优化执行顺序。
    优先级: Create Place > Create Character/Item > Modify/Transfer > Destroy
    """

    def get_priority(command_data: Dict[str, Any]) -> int:
        command = command_data.get("command", "").lower()
        entity_type = command_data.get("entity_type", "")

        if command == "create":
            if entity_type == "Place":
                return 0
            elif entity_type == "Character":
                return 1
            elif entity_type == "Item":
                return 2
        elif command in ["modify", "transfer"]:
            return 3
        elif command == "destroy":
            return 4
        return 99  # 未知指令放最后

    sorted_commands = sorted(commands, key=get_priority)

    original_order_summary = [(c.get('command'), c.get('entity_type', 'N/A')) for c in commands]
    sorted_order_summary = [(c.get('command'), c.get('entity_type', 'N/A')) for c in sorted_commands]

    if original_order_summary != sorted_order_summary:
        logging.info("指令已重新排序以优化执行:")
        for i, cmd in enumerate(sorted_commands):
            logging.info(
                f"  排序后 #{i + 1}: {cmd.get('command')} {cmd.get('entity_type', '')} {cmd.get('entity_id', '')}")

    return sorted_commands


# --- 指令执行函数 ---
def execute_parsed_commands(parsed_commands_list: List[Dict], game_state: GameState):
    """执行从文本中解析出的指令列表"""
    if not parsed_commands_list: return
    logging.info(f"准备执行 {len(parsed_commands_list)} 条指令...")

    # 执行指令
    for cmd_data in parsed_commands_list:
        command = cmd_data.get("command")

        entity_type = cmd_data.get("entity_type")
        entity_id = cmd_data.get("entity_id")
        params = cmd_data.get("params", {})

        if not command or not entity_type or not entity_id:
            # Create/Modify/Destroy/Transfer 都需要这三个
            if command != 'destroy':  # Destroy 只需要 type 和 id (虽然目前实现需要 command)
                logging.warning(f"跳过格式不完整的指令 (缺少 command/type/id): {cmd_data}")
                continue

        try:
            logging.debug(f"执行指令: {command} {entity_type} {entity_id} with params {params}")
            if command == "create":
                # 类型提示帮助检查
                game_state.execute_create(cast(Literal["Item", "Character", "Place"], entity_type), entity_id, params)
            elif command == "modify":
                game_state.execute_modify(cast(Literal["Item", "Character", "Place"], entity_type), entity_id, params)
            elif command == "destroy":
                game_state.execute_destroy(cast(Literal["Item", "Character", "Place"], entity_type), entity_id)
            elif command == "transfer":
                target_spec = params.get("target")
                if target_spec:
                    # entity_type 必须是 Item 或 Character
                    if entity_type not in ["Item", "Character"]:
                        raise TypeError(f"Transfer 指令只适用于 Item 或 Character，而非 {entity_type}")
                    game_state.execute_transfer(cast(Literal["Item", "Character"], entity_type), entity_id, target_spec)
                else:
                    logging.warning(f"跳过 Transfer 指令（缺少 target 参数）: {cmd_data}")
            # else: 未知指令已在 parser 中处理或被 sort 放到最后忽略

        except (ValueError, TypeError, KeyError) as e:
            logging.error(f"执行指令 {cmd_data} 时出错: {e}")
            print(f"[系统警告：执行指令 '{command} {entity_id if entity_id else ''}' 时出错: {e}]")
            # 原型阶段不崩溃，继续执行下一条指令
        except Exception as e:
            logging.exception(f"执行指令 {cmd_data} 时发生意外错误:")
            print(f"[系统严重错误：执行指令 '{command} {entity_id if entity_id else ''}' 时崩溃。请检查日志。]")
            raise e  # 让其崩溃

    logging.info("所有指令执行完毕。")

# == 后端逻辑工作者 ==
class AIWorker(QObject):
    chunk_received = Signal(str)
    finished = Signal(str)
    error = Signal(str)

    def __init__(self, system_prompt: str, history: List[Dict[str, str]], ai_service: AIService):
        super().__init__()
        self.system_prompt = system_prompt
        self.history = history
        self.ai_service = ai_service
        self._is_running = True

    @Slot()
    def run(self):
        if not self.ai_service or not self.ai_service.client:
            self.error.emit("AI 服务未初始化！")
            self.finished.emit("")
            return

        full_response = ""
        try:
            stream = self.ai_service.get_completion_stream(self.system_prompt, self.history)
            if stream:
                for chunk in stream:
                    if not self._is_running:
                        logging.warning("AIWorker 被外部停止。")
                        break
                    delta_content = getattr(getattr(getattr(chunk, 'choices', [{}])[0], 'delta', {}), 'content', None)
                    if delta_content:
                        self.chunk_received.emit(delta_content)
                        full_response += delta_content
            else:
                self.error.emit("未能获取 AI 响应流。")

        except Exception as e:
            error_message = f"AI 调用出错: {e}"
            logging.exception(error_message)
            self.error.emit(error_message)
        finally:
            # 确保即使出错或停止也发射 finished 信号
            self.finished.emit(full_response if self._is_running else "")  # 如果是中途停止，响应可能不完整

    def stop(self):
        self._is_running = False


# == 主窗口类 ==
class MainWindow(QMainWindow):
    append_text_signal = Signal(str)  # 安全更新主显示区
    set_status_signal = Signal(str)  # 安全更新状态栏
    clear_display_signal = Signal()  # 安全清空主显示区
    game_state_changed_signal = Signal()  # 通知游戏状态已改变 (用于未来更新左右栏)

    def __init__(self):
        super().__init__()
        self.setWindowTitle("AI RPG Engine - GUI Prototype")
        self.setGeometry(100, 100, 1200, 800)

        # --- 后端实例 ---
        self.ai_service = AIService()
        self.game_state = GameState(max_history=10)
        self.conversation_history: List[Dict[str, Any]] = []

        # --- AI 线程 ---
        self.ai_thread: Optional[QThread] = None
        self.ai_worker: Optional[AIWorker] = None

        # --- 创建 UI ---
        self._create_menu_bar()
        self._create_main_layout()
        self._connect_signals()

        self.statusBar().showMessage("准备就绪。请开始新游戏或加载存档。")
        # 可以在这里添加一个初始对话框让用户选择？或者在菜单实现。
        # 暂时让用户通过菜单操作。

    def _create_menu_bar(self):
        menu_bar = self.menuBar()
        file_menu = menu_bar.addMenu("文件")

        new_game_action = file_menu.addAction("新游戏")
        new_game_action.triggered.connect(self.start_new_game)

        save_action = file_menu.addAction("保存游戏...")  # ... 表示会弹出对话框
        save_action.triggered.connect(self.save_game_dialog)

        load_action = file_menu.addAction("加载游戏...")
        load_action.triggered.connect(self.load_game_dialog)

        file_menu.addSeparator()

        set_api_key_action = file_menu.addAction("设置 API Key...")
        set_api_key_action.triggered.connect(self.open_api_key_dialog)

        file_menu.addSeparator()
        exit_action = file_menu.addAction("退出")
        exit_action.triggered.connect(self.close)

        edit_menu = menu_bar.addMenu("编辑")
        rollback_action = edit_menu.addAction("撤销 (Rollback)")
        rollback_action.triggered.connect(self.rollback_action)
        commit_action = edit_menu.addAction("固化状态 (Commit)")
        commit_action.triggered.connect(self.commit_action)

        help_menu = menu_bar.addMenu("帮助")
        about_action = help_menu.addAction("关于")
        about_action.triggered.connect(self.show_about_dialog)

    def _create_main_layout(self):
        central_widget = QWidget()
        self.setCentralWidget(central_widget)
        main_layout = QHBoxLayout(central_widget)
        splitter = QSplitter(Qt.Orientation.Horizontal)

        self.left_panel = QWidget()
        # self.left_panel.setStyleSheet("background-color: lightgrey;")
        splitter.addWidget(self.left_panel)

        middle_panel = QWidget()
        middle_layout = QVBoxLayout(middle_panel)
        self.main_display = QTextEdit()
        self.main_display.setReadOnly(True)
        middle_layout.addWidget(self.main_display, 1)
        input_layout = QHBoxLayout()
        self.user_input = QTextEdit()
        self.user_input.setFixedHeight(80)  # 调整高度
        input_layout.addWidget(self.user_input, 1)
        self.send_button = QPushButton("发送")
        self.send_button.setFixedSize(80, 80)  # 调整大小
        input_layout.addWidget(self.send_button)
        middle_layout.addLayout(input_layout)
        splitter.addWidget(middle_panel)

        self.right_panel = QWidget()
        # self.right_panel.setStyleSheet("background-color: lightblue;")
        splitter.addWidget(self.right_panel)

        splitter.setSizes([200, 800, 200])
        main_layout.addWidget(splitter)

    def _connect_signals(self):
        self.send_button.clicked.connect(self.on_send_button_clicked)
        self.append_text_signal.connect(self._append_text_to_display)
        self.clear_display_signal.connect(self.main_display.clear)
        self.set_status_signal.connect(self.statusBar().showMessage)
        self.game_state_changed_signal.connect(self.update_side_panels)  # 连接状态更新

    # --- 槽函数和动作 ---
    @Slot()
    def start_new_game(self):
        # 可以添加确认对话框
        reply = QMessageBox.question(self, "新游戏", "确定要开始新游戏吗？未保存的进度将丢失。",
                                     QMessageBox.StandardButton.Yes | QMessageBox.StandardButton.No,
                                     QMessageBox.StandardButton.No)
        if reply == QMessageBox.StandardButton.Yes:
            logging.info("开始新游戏...")
            self.game_state = GameState(max_history=self.game_state.max_history)  # 重置状态
            self.conversation_history = [
                {"role": "system", "content": "开始新游戏。", "timestamp": datetime.datetime.now().isoformat(),
                 "type": "system_message"}]
            self.last_roll_results = []
            self.clear_display_signal.emit()  # 清空显示
            self._append_text_to_display("新游戏已开始。\n\n")
            self.set_status_signal.emit("新游戏开始，请输入你的行动。")
            self.game_state_changed_signal.emit()  # 触发界面更新

    @Slot()
    def save_game_dialog(self):
        # 使用 Qt 的文件保存对话框
        from PySide6.QtWidgets import QFileDialog
        save_path_tuple = QFileDialog.getSaveFileName(self, "保存游戏", ".", "JSON Files (*.json)")
        save_path_str = save_path_tuple[0]  # 获取文件路径

        if save_path_str:
            save_path = Path(save_path_str)
            if self.game_state.save_game(save_path, self.conversation_history):
                self.set_status_signal.emit(f"游戏已保存到 {save_path.name}")
                QMessageBox.information(self, "保存成功", f"游戏已成功保存到:\n{save_path}")
            else:
                self.set_status_signal.emit("保存游戏失败！")
                QMessageBox.critical(self, "保存失败", "保存游戏时发生错误，请检查日志。")

    @Slot()
    def load_game_dialog(self):
        # 使用 Qt 的文件打开对话框
        from PySide6.QtWidgets import QFileDialog
        load_path_tuple = QFileDialog.getOpenFileName(self, "加载游戏", ".", "JSON Files (*.json)")
        load_path_str = load_path_tuple[0]

        if load_path_str:
            load_path = Path(load_path_str)
            reply = QMessageBox.question(self, "加载游戏",
                                         f"确定要加载存档 '{load_path.name}' 吗？\n未保存的进度将丢失。",
                                         QMessageBox.StandardButton.Yes | QMessageBox.StandardButton.No,
                                         QMessageBox.StandardButton.No)
            if reply == QMessageBox.StandardButton.Yes:
                load_result = load_game(load_path)  # 调用加载逻辑
                if load_result:
                    self.game_state, self.conversation_history = load_result
                    self.last_roll_results = []  # 加载后重置待处理掷骰
                    self.clear_display_signal.emit()
                    self._append_text_to_display(f"存档 '{load_path.name}' 加载成功！\n\n--- 最近对话记录 ---\n")
                    # 显示最近记录
                    display_limit = 5
                    start_index = max(0, len(self.conversation_history) - display_limit * 2)
                    for msg in self.conversation_history[start_index:]:
                        role = msg.get('role', 'unknown')
                        content = msg.get('content', '')
                        if role == 'user':
                            self._append_text_to_display(f"你: {content}\n")
                        elif role == 'assistant':
                            cleaned_content = re.sub(r"@[a-zA-Z]+[^\n@]*?(?=\n|@|$)", "", content,
                                                     flags=re.MULTILINE).strip()
                            self._append_text_to_display(
                                f"AI: {cleaned_content[:150]}{'...' if len(cleaned_content) > 150 else ''}\n")
                    self._append_text_to_display("----------------------\n\n")
                    self.set_status_signal.emit(f"存档 '{load_path.name}' 加载成功。")
                    self.game_state_changed_signal.emit()  # 触发界面更新
                else:
                    self.set_status_signal.emit(f"加载存档 '{load_path.name}' 失败！")
                    QMessageBox.critical(self, "加载失败", f"加载存档时发生错误:\n{load_path}\n请检查文件或日志。")

    @Slot()
    def rollback_action(self):
        if self.ai_thread and self.ai_thread.isRunning():
            QMessageBox.warning(self, "操作失败", "无法在 AI 响应期间回滚。")
            return
        if self.game_state.rollback_state():
            self._append_text_to_display("\n[系统: 操作已撤销，状态已回滚]\n\n")
            self.set_status_signal.emit("状态已回滚。")
            # 可能需要移除 conversation_history 中的最后几条记录？
            # 简单的做法是不移除，但 AI 看到的 history 会基于旧状态生成
            self.game_state_changed_signal.emit()  # 触发界面更新
        else:
            self.set_status_signal.emit("无法回滚（没有历史记录）。")
            QMessageBox.information(self, "无法回滚", "没有更多可用的历史状态。")

    @Slot()
    def commit_action(self):
        if self.ai_thread and self.ai_thread.isRunning():
            QMessageBox.warning(self, "操作失败", "无法在 AI 响应期间固化状态。")
            return
        reply = QMessageBox.question(self, "固化状态", "确定要清除所有撤销历史吗？此操作不可逆。",
                                     QMessageBox.StandardButton.Yes | QMessageBox.StandardButton.No,
                                     QMessageBox.StandardButton.No)
        if reply == QMessageBox.StandardButton.Yes:
            self.game_state.commit_state()
            self._append_text_to_display("\n[系统: 当前状态已固化，历史记录已清除]\n\n")
            self.set_status_signal.emit("状态已固化，历史记录已清除。")

    @Slot()
    def open_api_key_dialog(self):
        # 更健壮地获取当前 key
        current_key = ""
        if self.ai_service and self.ai_service.client:
            try:
                current_key = self.ai_service.client.api_key
            except Exception:
                pass  # 忽略获取错误

        key, ok = QInputDialog.getText(self, "设置 API Key", "请输入 DeepSeek API Key:", QLineEdit.Password, current_key)
        if ok and key:
            # **重要**: 需要重新初始化 AIService 或更新其内部 client
            # 简单的做法是重新创建实例
            try:
                self.ai_service = AIService(api_key=key)
                if self.ai_service.client:
                    QMessageBox.information(self, "API Key", "API Key 已更新。")
                    self.set_status_signal.emit("API Key 已更新。")
                else:
                    QMessageBox.warning(self, "API Key 错误",
                                        "更新 API Key 后未能成功初始化 AI 服务。请检查 Key 是否有效。")
                    self.set_status_signal.emit("更新 API Key 失败！")
            except Exception as e:
                QMessageBox.critical(self, "错误", f"设置 API Key 时出错: {e}")
                logging.error(f"设置 API Key 时出错: {e}", exc_info=True)

    @Slot()
    def show_about_dialog(self):
        QMessageBox.about(self, "关于", "AI RPG Engine - GUI 原型\n版本 0.1.0\n一个实验性的项目。")

    @Slot()
    def on_send_button_clicked(self):
        if self.ai_thread and self.ai_thread.isRunning():
            logging.warning("用户尝试在 AI 处理期间发送消息，已忽略。")
            return  # 防止重复发送

        user_text = self.user_input.toPlainText().strip()
        if not user_text:
            return

        self.user_input.setEnabled(False)
        self.send_button.setEnabled(False)
        self.set_status_signal.emit("正在与 AI 通信...")

        self._append_text_to_display(f"你: {user_text}\n\n")

        self.conversation_history.append({
            "role": "user", "content": user_text,
            "timestamp": datetime.datetime.now().isoformat(), "type": "user_input"
        })

        # --- AI 调用准备 ---
        ai_history_for_prompt = prompts.clean_history_for_ai(self.conversation_history)
        # 获取提示词，包含上一轮掷骰结果
        system_prompt = prompts.get_system_prompt(self.game_state)
        # 清空待处理掷骰结果
        self.last_roll_results = []

        # --- 启动线程 ---
        self.ai_thread = QThread()
        self.ai_worker = AIWorker(system_prompt, ai_history_for_prompt, self.ai_service)
        self.ai_worker.moveToThread(self.ai_thread)
        # 连接信号
        self.ai_worker.chunk_received.connect(self.on_ai_chunk_received)
        self.ai_worker.finished.connect(self.on_ai_finished)
        self.ai_worker.error.connect(self.on_ai_error)
        self.ai_thread.started.connect(self.ai_worker.run)
        self.ai_thread.finished.connect(self.ai_thread.deleteLater)
        self.ai_worker.finished.connect(self.ai_thread.quit)
        self.ai_worker.finished.connect(self.ai_worker.deleteLater)
        self.ai_thread.start()

        self.user_input.clear()

    @Slot(str)
    def on_ai_chunk_received(self, chunk: str):
        self.append_text_signal.emit(chunk)

    @Slot(str)
    def on_ai_finished(self, full_response: str):
        logging.info("AI 响应接收完毕。")
        if full_response:
            self.conversation_history.append({
                "role": "assistant", "content": full_response,
                "timestamp": datetime.datetime.now().isoformat(), "type": "ai_response"
            })
            # --- 解析和执行指令 ---
            try:
                parsed_commands = parse_commands(full_response)
                if parsed_commands:
                    logging.info(f"解析到 {len(parsed_commands)} 条指令，准备执行...")
                    # --- 保存快照 ---
                    self.game_state.save_history_point()
                    # --- 排序和执行 ---
                    sorted_commands = sort_commands_for_execution(parsed_commands)
                    execute_parsed_commands(sorted_commands, self.game_state)
                    logging.info("指令执行完毕。")
                    # --- 通知状态改变 ---
                    self.game_state_changed_signal.emit()

            except Exception as e:
                error_msg = f"处理 AI 响应或执行指令时出错: {e}"
                logging.error(error_msg, exc_info=True)
                self.append_text_signal.emit(f"\n[系统错误: {error_msg}]\n建议使用 /rollback。\n\n")
                # 执行出错，不清除掷骰结果，不清空 history snapshot (如果执行前保存了)
                # 可以考虑自动回滚
                # if self.game_state.rollback_state(): self.append_text_signal.emit("[系统提示: 已自动回滚]\n")

        self.append_text_signal.emit("\n")  # 在 AI 回复后加一个空行
        self.main_display.ensureCursorVisible()

        # 恢复输入状态
        self.user_input.setEnabled(True)
        self.send_button.setEnabled(True)
        self.set_status_signal.emit("轮到你了。")
        self.user_input.setFocus()

        self.ai_thread = None
        self.ai_worker = None

    @Slot(str)
    def on_ai_error(self, error_message: str):
        logging.error(f"AIWorker 发出错误信号: {error_message}")
        self.append_text_signal.emit(f"\n[系统错误: {error_message}]\n\n")
        self.main_display.ensureCursorVisible()
        self.user_input.setEnabled(True)
        self.send_button.setEnabled(True)
        self.set_status_signal.emit(f"错误: {error_message[:50]}...")
        self.user_input.setFocus()
        if self.ai_thread: self.ai_thread.quit()
        self.ai_thread = None
        self.ai_worker = None

    @Slot(str)
    def _append_text_to_display(self, text: str):
        cursor = self.main_display.textCursor()
        cursor.movePosition(QtGui.QTextCursor.MoveOperation.End)
        cursor.insertText(text)
        self.main_display.ensureCursorVisible()  # 滚动到底部

    @Slot()
    def update_side_panels(self):
        # --- 占位符：未来在这里更新左右栏 ---
        logging.debug("信号触发：更新侧边栏 (占位符)")
        # 示例：更新窗口标题显示焦点
        current_focus = self.game_state.get_current_focus()
        focus_str = f"焦点: {', '.join(current_focus)}" if current_focus else "无焦点"
        self.setWindowTitle(f"AI RPG Engine - {focus_str}")
        # 未来在这里添加代码，从 game_state 获取数据填充 QListWidget 等

    def closeEvent(self, event: QtGui.QCloseEvent):  # 指定事件类型
        logging.info("收到关闭窗口事件。")
        # 可以添加“是否保存”的提示
        reply = QMessageBox.question(self, "退出确认", "确定要退出吗？\n未保存的进度将丢失。",
                                     QMessageBox.StandardButton.Yes | QMessageBox.StandardButton.No,
                                     QMessageBox.StandardButton.No)

        if reply == QMessageBox.StandardButton.Yes:
            if self.ai_thread and self.ai_thread.isRunning():
                logging.info("正在尝试停止 AI 线程...")
                if self.ai_worker: self.ai_worker.stop()
                self.ai_thread.quit()
                if not self.ai_thread.wait(1000):
                    logging.warning("AI 线程未能优雅地停止。")
            logging.info("接受关闭事件，退出程序。")
            event.accept()
        else:
            logging.info("取消关闭事件。")
            event.ignore()


# --- 应用程序入口 ---
if __name__ == "__main__":
    app = QApplication(sys.argv)
    # 设置应用程序信息 (可选)
    app.setApplicationName("AI RPG Engine")
    app.setOrganizationName("YourNameOrOrg")  # 可选
    window = MainWindow()
    window.show()
    sys.exit(app.exec())
