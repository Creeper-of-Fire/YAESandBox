# main_gui.py
import datetime
import logging
import re
import sys
from pathlib import Path
from typing import List, Dict, Optional, Any

from PySide6 import QtGui
from PySide6.QtCore import Qt, QThread, Signal, Slot, QObject
from PySide6.QtWidgets import (
    QApplication, QMainWindow, QWidget, QHBoxLayout, QVBoxLayout,
    QTextEdit, QPushButton, QLineEdit, QMessageBox, QSplitter,
    QInputDialog, QFileDialog
)

import prompts
from ai_service import AIService
from command_processor import CommandExecutor  # 导入执行器
from game_state import GameState, load_game  # GameState 现在不含执行逻辑
from parser import parse_commands
from world_state import BaseEntity, Item, Character, Place  # 导入实体类用于类型检查和属性访问

logging.basicConfig(level=logging.INFO, format='%(asctime)s - %(levelname)s - [%(filename)s:%(lineno)d] - %(message)s')


# AIWorker (保持不变) ...
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
            logging.debug("AIWorker: 开始调用 AI 服务")
            stream = self.ai_service.get_completion_stream(self.system_prompt, self.history)
            if stream:
                logging.debug("AIWorker: 成功获取流")
                for chunk in stream:
                    if not self._is_running:
                        logging.warning("AIWorker: 收到停止信号")
                        break
                    choices = getattr(chunk, 'choices', [])
                    if choices:
                        delta = getattr(choices[0], 'delta', None)
                        if delta:
                            delta_content = getattr(delta, 'content', None)
                            if delta_content:
                                self.chunk_received.emit(delta_content)
                                full_response += delta_content
                logging.debug("AIWorker: 流接收完毕。")
            else:
                logging.error("AIWorker:未能获取 AI 响应流。")
                self.error.emit("未能获取 AI 响应流。")
        except Exception as e:
            error_message = f"AI 调用出错: {e}"
            logging.exception("AIWorker: 捕获到异常:")
            self.error.emit(error_message)
        finally:
            logging.debug(f"AIWorker: run 结束，Is Running: {self._is_running}")
            self.finished.emit(full_response if self._is_running else "")

    def stop(self):
        logging.debug("AIWorker: stop 调用。")
        self._is_running = False


# 主窗口类
class MainWindow(QMainWindow):
    append_text_signal = Signal(str)
    set_status_signal = Signal(str)
    clear_display_signal = Signal()
    game_state_changed_signal = Signal()

    def __init__(self):
        super().__init__()
        self.setWindowTitle("AI RPG Engine - GUI Refactored v2")
        self.setGeometry(100, 100, 1200, 800)
        self.ai_service = AIService()
        self.game_state = GameState(max_history=10)
        self.conversation_history: List[Dict[str, Any]] = []
        self.ai_thread: Optional[QThread] = None
        self.ai_worker: Optional[AIWorker] = None
        self._create_menu_bar()
        self._create_main_layout()
        self._connect_signals()
        self.statusBar().showMessage("准备就绪。")

    def _create_menu_bar(self):  # (保持不变)
        menu_bar = self.menuBar()
        file_menu = menu_bar.addMenu("文件")
        new_game_action = file_menu.addAction("新游戏")
        new_game_action.triggered.connect(self.start_new_game)
        save_action = file_menu.addAction("保存游戏...")
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

    def _create_main_layout(self):  # (保持不变)
        central_widget = QWidget()
        self.setCentralWidget(central_widget)
        main_layout = QHBoxLayout(central_widget)
        splitter = QSplitter(Qt.Orientation.Horizontal)
        self.left_panel = QTextEdit("左侧面板 (角色/地点信息)")
        self.left_panel.setReadOnly(True)
        splitter.addWidget(self.left_panel)
        middle_panel = QWidget()
        middle_layout = QVBoxLayout(middle_panel)
        self.main_display = QTextEdit()
        self.main_display.setReadOnly(True)
        middle_layout.addWidget(self.main_display, 1)
        input_layout = QHBoxLayout()
        self.user_input = QTextEdit()
        self.user_input.setFixedHeight(80)
        self.user_input.setPlaceholderText("在此输入你的行动...")
        input_layout.addWidget(self.user_input, 1)
        self.send_button = QPushButton("发送")
        self.send_button.setFixedSize(80, 80)
        input_layout.addWidget(self.send_button)
        middle_layout.addLayout(input_layout)
        splitter.addWidget(middle_panel)
        self.right_panel = QTextEdit("右侧面板 (物品栏/状态)")
        self.right_panel.setReadOnly(True)
        splitter.addWidget(self.right_panel)
        splitter.setSizes([250, 700, 250])
        main_layout.addWidget(splitter)

    def _connect_signals(self):  # (保持不变)
        self.send_button.clicked.connect(self.on_send_button_clicked)
        self.append_text_signal.connect(self._append_text_to_display)
        self.clear_display_signal.connect(self.main_display.clear)
        self.set_status_signal.connect(self.statusBar().showMessage)
        self.game_state_changed_signal.connect(self.update_side_panels)

    @Slot()
    def start_new_game(self):  # (保持不变)
        if self.ai_thread and self.ai_thread.isRunning():
            QMessageBox.warning(self, "操作失败", "请等待当前 AI 响应完成。")
            return
        reply = QMessageBox.question(self, "新游戏", "确定要开始新游戏吗？",
                                     QMessageBox.StandardButton.Yes | QMessageBox.StandardButton.No, QMessageBox.StandardButton.No)
        if reply == QMessageBox.StandardButton.Yes:
            logging.info("开始新游戏...")
            self.game_state = GameState(max_history=self.game_state.max_history)
            self.conversation_history = [
                {"role": "system", "content": "开始新游戏。", "timestamp": datetime.datetime.now().isoformat(), "type": "system_message"}]
            self.clear_display_signal.emit()
            self._append_text_to_display("新游戏已开始。\n请描述你的角色和初始场景...\n\n")
            self.set_status_signal.emit("新游戏开始")
            self.game_state_changed_signal.emit()

    @Slot()
    def save_game_dialog(self):  # (保持不变)
        if self.ai_thread and self.ai_thread.isRunning():
            QMessageBox.warning(self, "操作失败", "请等待 AI 响应。")
            return
        save_path_tuple = QFileDialog.getSaveFileName(self, "保存游戏", ".", "JSON Files (*.json)")
        if save_path_str := save_path_tuple[0]:
            save_path = Path(save_path_str)
            try:
                self.game_state.save_game(save_path, self.conversation_history)
                self.set_status_signal.emit(f"游戏已保存到 {save_path.name}")
                QMessageBox.information(self, "保存成功", f"游戏已保存:\n{save_path}")
            except Exception as e:
                logging.error(f"保存游戏时出错: {e}", exc_info=True)
                self.set_status_signal.emit(f"保存失败: {e}")
                QMessageBox.critical(self, "保存失败", f"保存出错:\n{e}")

    @Slot()
    def load_game_dialog(self):  # (检查 name 访问 - 使用了 property name，安全)
        if self.ai_thread and self.ai_thread.isRunning():
            QMessageBox.warning(self, "操作失败", "请等待 AI 响应。")
            return
        load_path_tuple = QFileDialog.getOpenFileName(self, "加载游戏", ".", "JSON Files (*.json)")
        if load_path_str := load_path_tuple[0]:
            load_path = Path(load_path_str)
            reply = QMessageBox.question(self, "加载游戏", f"确定加载 '{load_path.name}' 吗？", QMessageBox.StandardButton.Yes | QMessageBox.StandardButton.No,
                                         QMessageBox.StandardButton.No)
            if reply == QMessageBox.StandardButton.Yes:
                try:
                    loaded_game_state, loaded_history = load_game(load_path)
                    self.game_state = loaded_game_state
                    self.conversation_history = loaded_history
                    self.clear_display_signal.emit()
                    self._append_text_to_display(f"存档 '{load_path.name}' 加载成功！\n\n--- 最近对话记录 ---\n")
                    display_limit = 5
                    start_index = max(0, len(self.conversation_history) - display_limit * 2)
                    for msg in self.conversation_history[start_index:]:
                        role = msg.get('role', 'unknown')
                        content = msg.get('content', '')
                        timestamp_str = msg.get('timestamp', '')
                        time_display = ""
                        try:
                            time_display = f"[{datetime.fromisoformat(timestamp_str).strftime('%H:%M')}] " if timestamp_str else ""
                        except ValueError:
                            pass
                        if role == 'user':
                            self._append_text_to_display(f"{time_display}你: {content}\n")
                        elif role == 'assistant':
                            cleaned_content = re.sub(r"@[a-zA-Z]+.*?(?:\n|\Z)", "", content, flags=re.MULTILINE).strip()
                            if cleaned_content: self._append_text_to_display(
                                f"{time_display}AI: {cleaned_content[:200]}{'...' if len(cleaned_content) > 200 else ''}\n")
                    self._append_text_to_display("\n----------------------\n请继续游戏。\n")
                    self.set_status_signal.emit(f"存档 '{load_path.name}' 加载成功。")
                    self.game_state_changed_signal.emit()
                except Exception as e:
                    logging.error(f"加载存档时出错: {e}", exc_info=True)
                    self.set_status_signal.emit(f"加载失败: {e}")
                    QMessageBox.critical(self, "加载失败",
                                         f"加载出错:\n{e}")

    @Slot()
    def rollback_action(self):  # (保持不变)
        if self.ai_thread and self.ai_thread.isRunning():
            QMessageBox.warning(self, "操作失败", "无法在 AI 响应期间回滚。")
            return
        if self.game_state.rollback_state():
            self._append_text_to_display("\n[系统: 世界状态已回滚]\n\n")
            self.set_status_signal.emit("状态已回滚。")
            self.game_state_changed_signal.emit()
        else:
            self.set_status_signal.emit("无法回滚（没有历史记录）。")
            QMessageBox.information(self, "无法回滚", "没有更多历史状态。")

    @Slot()
    def commit_action(self):  # (保持不变)
        if self.ai_thread and self.ai_thread.isRunning():
            QMessageBox.warning(self, "操作失败", "无法在 AI 响应期间固化。")
            return
        reply = QMessageBox.question(self, "固化状态", "确定清除所有撤销历史吗？", QMessageBox.StandardButton.Yes | QMessageBox.StandardButton.No,
                                     QMessageBox.StandardButton.No)
        if reply == QMessageBox.StandardButton.Yes:
            self.game_state.commit_state()
            self._append_text_to_display("\n[系统: 历史记录已清除]\n\n")
            self.set_status_signal.emit("状态已固化。")

    @Slot()
    def open_api_key_dialog(self):  # (保持不变)
        current_key = ""
        if self.ai_service and self.ai_service.client:
            current_key = self.ai_service.client.api_key or ""
        key, ok = QInputDialog.getText(self, "设置 API Key", "请输入 DeepSeek API Key:", QLineEdit.Password, current_key)
        if ok and key:
            temp_ai_service = AIService(api_key=key)
            if temp_ai_service.client:
                self.ai_service = temp_ai_service
                QMessageBox.information(self, "API Key", "API Key 已更新。")
                self.set_status_signal.emit("API Key 已更新。")
            else:
                QMessageBox.warning(self, "API Key 错误", "未能初始化 AI 服务。")
                self.set_status_signal.emit("更新 API Key 失败！")

    @Slot()
    def show_about_dialog(self):  # (保持不变)
        QMessageBox.about(self, "关于", "AI RPG Engine - GUI Refactored v2\n版本 0.4.2\n实验项目。")

    @Slot()
    def on_send_button_clicked(self):  # (保持不变)
        if self.ai_thread and self.ai_thread.isRunning():
            logging.warning("忽略重复发送")
            QMessageBox.information(self, "请稍候", "请等待 AI 响应。")
            return
        user_text = self.user_input.toPlainText().strip()
        if not user_text: return
        self.user_input.setEnabled(False)
        self.send_button.setEnabled(False)
        self.set_status_signal.emit("与 AI 通信...")
        self._append_text_to_display(f"\n你: {user_text}\n\n")
        self.conversation_history.append({"role": "user", "content": user_text, "timestamp": datetime.datetime.now().isoformat(), "type": "user_input"})
        self.user_input.clear()
        try:
            ai_history_for_prompt = prompts.clean_history_for_ai(self.conversation_history)
            system_prompt = prompts.get_system_prompt(self.game_state)
            self.ai_thread = QThread(self)
            self.ai_worker = AIWorker(system_prompt, ai_history_for_prompt, self.ai_service)
            self.ai_worker.moveToThread(self.ai_thread)
            self.ai_worker.chunk_received.connect(self.on_ai_chunk_received)
            self.ai_worker.finished.connect(self.on_ai_finished)
            self.ai_worker.error.connect(self.on_ai_error)
            self.ai_thread.started.connect(self.ai_worker.run)
            self.ai_worker.finished.connect(self.ai_thread.quit)
            self.ai_worker.finished.connect(self.ai_worker.deleteLater)
            self.ai_thread.finished.connect(self.ai_thread.deleteLater)
            logging.debug("启动 AIWorker 线程...")
            self.ai_thread.start()
        except Exception as e:
            logging.error(f"准备 AI 调用或启动线程时出错: {e}", exc_info=True)
            self._append_text_to_display(f"\n[系统错误: 无法开始 AI 通信 - {e}]\n")
            self.user_input.setEnabled(True)
            self.send_button.setEnabled(True)
            self.set_status_signal.emit(f"错误: {e}")
            self.user_input.setFocus()

    @Slot(str)
    def on_ai_chunk_received(self, chunk: str):  # (保持不变)
        self.append_text_signal.emit(chunk)

    @Slot(str)
    def on_ai_finished(self, full_response: str):  # (调用 CommandExecutor)
        logging.info("AI 响应接收完毕。")
        self._append_text_to_display("\n")
        self.main_display.ensureCursorVisible()
        if full_response:
            self.conversation_history.append(
                {"role": "assistant", "content": full_response, "timestamp": datetime.datetime.now().isoformat(), "type": "ai_response"})
            try:  # --- 解析和执行指令 ---
                parsed_commands = parse_commands(full_response)
                if parsed_commands:
                    logging.info(f"解析到 {len(parsed_commands)} 条指令，准备执行...")
                    self.game_state.save_history_point()  # 保存快照
                    # 调用 CommandExecutor 执行指令，传入 world 对象
                    CommandExecutor.execute_commands(parsed_commands, self.game_state.world)
                    logging.info("指令执行完毕。")
                    self.game_state_changed_signal.emit()  # 通知 UI 更新
                else:
                    logging.info("AI 响应中未找到指令。")
            except Exception as e:
                error_msg = f"处理 AI 响应或执行指令时出错: {e}"
                logging.error(error_msg, exc_info=True)
                self._append_text_to_display(f"\n[系统错误: {error_msg}]\n建议检查 AI 输出或使用 '撤销' 功能。\n\n")
                # 可以在此添加自动回滚
                # if self.game_state.rollback_state(): self._append_text_to_display("[系统提示: 已自动回滚]\n")
        self.user_input.setEnabled(True)
        self.send_button.setEnabled(True)
        self.set_status_signal.emit("轮到你了。")
        self.user_input.setFocus()
        self.ai_thread = None
        self.ai_worker = None
        logging.debug("AI 流程处理完毕，UI 已恢复。")

    @Slot(str)
    def on_ai_error(self, error_message: str):  # (保持不变)
        logging.error(f"AIWorker 发出错误信号: {error_message}")
        self._append_text_to_display(f"\n[AI 通信错误: {error_message}]\n\n")
        self.main_display.ensureCursorVisible()
        self.user_input.setEnabled(True)
        self.send_button.setEnabled(True)
        self.set_status_signal.emit(f"AI 错误: {error_message[:50]}...")
        self.user_input.setFocus()
        self.ai_thread = None
        self.ai_worker = None
        logging.debug("AI 错误处理完毕，UI 已恢复。")

    @Slot(str)
    def _append_text_to_display(self, text: str):  # (保持不变)
        cursor = self.main_display.textCursor()
        cursor.movePosition(QtGui.QTextCursor.MoveOperation.End)
        cursor.insertText(text)
        self.main_display.ensureCursorVisible()

    @Slot()
    def update_side_panels(self):  # (检查 name 访问 - 使用了 property name，安全)
        logging.debug("信号触发：更新侧边栏")
        current_focus_ids = self.game_state.get_current_focus()
        focus_str = "无焦点"
        if current_focus_ids:
            focus_names = [entity.name if (entity := self.game_state.find_entity(fid)) else f"<{fid}>" for fid in current_focus_ids]
            focus_str = f"焦点: {', '.join(focus_names)}"
        self.setWindowTitle(f"AI RPG Engine - {focus_str}")
        left_content = "焦点实体:\n----------------\n"
        right_content = "持有物品:\n----------------\n"
        if not current_focus_ids:
            left_content += "(无)"
        else:
            for fid in current_focus_ids:
                entity = self.game_state.find_entity(fid)
                if entity:
                    left_content += f"ID: {fid}\n类型: {entity.entity_type}\n名称: {entity.name}\n"  # 使用 entity.name
                    attrs = entity.get_all_attributes()
                    attrs.pop('name', None)  # 获取属性并移除已显示的 name
                    core_fields_to_exclude = BaseEntity._CORE_FIELDS.union({'entity_id', 'entity_type', 'name'})  # 排除所有核心字段和 name
                    dynamic_attrs = {k: v for k, v in attrs.items() if k not in core_fields_to_exclude and k not in entity.model_fields}  # 过滤出动态属性
                    if dynamic_attrs:
                        left_content += "动态属性:\n" + "".join(f"  - {k}: {repr(v)}\n" for k, v in dynamic_attrs.items())
                    # 显示特定类型的核心结构属性
                    if isinstance(entity, Item):
                        left_content += f"  - quantity: {entity.quantity}\n  - location: {entity.location}\n"
                    elif isinstance(entity, Character):
                        left_content += f"  - current_place: {entity.current_place}\n  - has_items: {entity.has_items}\n"
                    elif isinstance(entity, Place):
                        left_content += f"  - contents: {entity.contents}\n  - exits: {entity.exits}\n"
                    left_content += "----------------\n"
                    if isinstance(entity, Character):  # 更新右侧物品栏
                        right_content += f"[{entity.name} ({fid})]:\n"  # 使用 entity.name
                        if not entity.has_items:
                            right_content += "  (空)\n"
                        else:
                            right_content += "".join(f"  - {item.name}{f' (x{item.quantity})' if item.quantity > 1 else ''} ({item_id})\n" if (
                                                                                                                                                  item := self.game_state.find_entity(
                                                                                                                                                      item_id)) and isinstance(
                                item, Item) else f"  - <{item_id}> (物品丢失?)\n" for item_id in entity.has_items)
                        right_content += "----------------\n"
                else:
                    left_content += f"ID: {fid} (无效或已销毁)\n----------------\n"
        if not any(isinstance(self.game_state.find_entity(fid), Character) for fid in current_focus_ids): right_content += "(未聚焦角色)"
        self.left_panel.setText(left_content)
        self.right_panel.setText(right_content)

    def closeEvent(self, event: QtGui.QCloseEvent):  # (保持不变)
        logging.info("收到关闭事件。")
        reply = QMessageBox.question(self, "退出确认", "确定退出吗？", QMessageBox.StandardButton.Yes | QMessageBox.StandardButton.No,
                                     QMessageBox.StandardButton.No)
        if reply == QMessageBox.StandardButton.Yes:
            if self.ai_thread and self.ai_thread.isRunning():
                logging.info("停止 AI 线程...")
                if self.ai_worker:
                    self.ai_worker.stop()
                self.ai_thread.quit()
                if not self.ai_thread.wait(1000):
                    logging.warning("AI 线程未能优雅停止。")
                else:
                    logging.info("AI 线程已停止。")
            logging.info("接受关闭事件。")
            event.accept()
        else:
            logging.info("取消关闭事件。")
            event.ignore()


# --- 应用程序入口 ---
if __name__ == "__main__":
    app = QApplication(sys.argv)
    app.setApplicationName("AI RPG Engine")
    app.setOrganizationName("YourNameOrOrg")
    # API Key 检查 (如果需要)
    window = MainWindow()
    window.show()
    sys.exit(app.exec())
