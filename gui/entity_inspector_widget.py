# gui/entity_inspector_widget.py
import logging
import yaml # 使用 YAML 格式化显示属性，更清晰
from typing import Optional, Dict, Any, List

from PySide6.QtCore import Slot, Qt
from PySide6.QtWidgets import (QWidget, QVBoxLayout, QHBoxLayout, QLabel,
                             QLineEdit, QPushButton, QTextEdit, QMessageBox,
                             QSizePolicy)

# 导入服务接口
try:
    from services.interfaces import (IGameStateProvider, ICommandSubmitter,
                                     IWorkflowEngine, WorkflowCallback)
except ImportError:
    logging.error("CRITICAL: 无法导入服务接口! 请确保 services.interfaces.py 存在且路径正确。")
    # 提供假的接口以便代码能运行，但在实际应用中会失败
    class IGameStateProvider: pass
    class ICommandSubmitter: pass
    class IWorkflowEngine: pass
    WorkflowCallback = lambda: None # type: ignore


class EntityInspectorWidget(QWidget):
    """
    一个用于检查和与单个游戏实体交互的视图部件。
    """
    def __init__(self,
                 game_state_provider: IGameStateProvider,
                 command_submitter: ICommandSubmitter,
                 workflow_engine: IWorkflowEngine,
                 parent: Optional[QWidget] = None):
        """
        初始化实体检查器视图。

        Args:
            game_state_provider: 提供游戏状态访问的服务。
            command_submitter: 提交指令的服务。
            workflow_engine: 执行工作流的服务。
            parent: 父部件。
        """
        super().__init__(parent)
        self.logger = logging.getLogger(__name__)

        # --- 注入依赖 ---
        if not all([game_state_provider, command_submitter, workflow_engine]):
            raise ValueError("EntityInspectorWidget 必须接收有效的服务实例!")
        self.game_state_provider = game_state_provider
        self.command_submitter = command_submitter
        self.workflow_engine = workflow_engine

        self._current_entity_id: Optional[str] = None
        self._current_entity_type: Optional[str] = None

        self._init_ui()
        self._connect_signals()

        self.logger.debug("EntityInspectorWidget 初始化完毕。")

    def _init_ui(self):
        """初始化用户界面。"""
        main_layout = QVBoxLayout(self)

        # --- 输入区 ---
        input_layout = QHBoxLayout()
        input_layout.addWidget(QLabel("实体 ID:"))
        self.id_input = QLineEdit()
        self.id_input.setPlaceholderText("输入要检查的实体 ID")
        input_layout.addWidget(self.id_input)
        self.inspect_button = QPushButton("检查")
        input_layout.addWidget(self.inspect_button)
        main_layout.addLayout(input_layout)

        # --- 显示区 ---
        self.display_area = QTextEdit()
        self.display_area.setReadOnly(True)
        self.display_area.setPlaceholderText("实体属性将显示在这里...")
        self.display_area.setSizePolicy(QSizePolicy.Policy.Expanding, QSizePolicy.Policy.Expanding)
        main_layout.addWidget(self.display_area)

        # --- 操作区 ---
        action_layout = QHBoxLayout()
        self.generate_desc_button = QPushButton("生成描述 (AI)")
        self.generate_desc_button.setEnabled(False) # 默认禁用
        self.generate_desc_button.setToolTip("使用 AI 为当前实体生成更详细的描述")
        action_layout.addWidget(self.generate_desc_button)

        self.mark_destroyed_button = QPushButton("标记销毁")
        self.mark_destroyed_button.setEnabled(False) # 默认禁用
        self.mark_destroyed_button.setToolTip("将当前实体标记为已销毁状态")
        action_layout.addWidget(self.mark_destroyed_button)
        main_layout.addLayout(action_layout)

        self.logger.debug("EntityInspectorWidget UI 初始化完毕。")

    def _connect_signals(self):
        """连接信号和槽。"""
        self.inspect_button.clicked.connect(self._load_entity_info)
        self.id_input.returnPressed.connect(self._load_entity_info)
        self.generate_desc_button.clicked.connect(self._request_ai_description)
        self.mark_destroyed_button.clicked.connect(self._mark_entity_destroyed)
        self.logger.debug("EntityInspectorWidget 信号连接完毕。")

    @Slot()
    def _load_entity_info(self):
        """根据输入框中的 ID 加载并显示实体信息。"""
        entity_id = self.id_input.text().strip()
        self.display_area.clear()
        self._current_entity_id = None
        self._current_entity_type = None
        self.generate_desc_button.setEnabled(False)
        self.mark_destroyed_button.setEnabled(False)

        if not entity_id:
            self.display_area.setPlaceholderText("请输入实体 ID。")
            return

        self.logger.info(f"尝试加载实体 ID: {entity_id}")
        try:
            entity = self.game_state_provider.find_entity(entity_id, include_destroyed=True) # 允许查看已销毁实体
            if entity:
                self._current_entity_id = entity.entity_id
                self._current_entity_type = entity.entity_type
                all_attrs = entity.get_all_attributes()
                # 使用 YAML 格式化输出，更易读
                try:
                    display_text = f"# 实体: {entity_id} (类型: {entity.entity_type})\n"
                    display_text += yaml.dump(all_attrs, allow_unicode=True, default_flow_style=False, sort_keys=False)
                except Exception as e:
                    self.logger.warning(f"格式化实体属性为 YAML 时出错: {e}", exc_info=True)
                    display_text = f"实体: {entity_id}\n类型: {entity.entity_type}\n属性:\n" + "\n".join(f"  {k}: {v}" for k, v in all_attrs.items())

                self.display_area.setText(display_text)

                # 只有未销毁的实体才能执行操作
                if not entity.is_destroyed:
                    self.generate_desc_button.setEnabled(True)
                    self.mark_destroyed_button.setEnabled(True)
                else:
                     self.display_area.append("\n-- 实体已被销毁 --")

                self.logger.debug(f"成功加载并显示实体: {entity_id}")
            else:
                self.display_area.setText(f"# 未找到实体: {entity_id}")
                self.logger.warning(f"未找到实体 ID: {entity_id}")

        except Exception as e:
            self.logger.error(f"加载实体 '{entity_id}' 时发生错误: {e}", exc_info=True)
            self.display_area.setText(f"# 加载实体时出错:\n{e}")
            QMessageBox.critical(self, "错误", f"加载实体时发生错误:\n{e}")

    @Slot()
    def _request_ai_description(self):
        """请求工作流引擎为当前实体生成描述。"""
        if not self._current_entity_id:
            self.logger.warning("尝试为无效实体请求描述。")
            return

        self.logger.info(f"为实体 '{self._current_entity_id}' 请求 AI 生成描述...")
        self.generate_desc_button.setEnabled(False) # 暂时禁用按钮
        self.display_area.append("\n-- 正在向 AI 请求描述... --")

        context = {"entity_id": self._current_entity_id}
        try:
            # 调用工作流引擎，传入回调函数
            self.workflow_engine.execute_workflow(
                config_id="generate_description", # 假设工作流配置 ID 为这个
                context=context,
                callback=self._handle_ai_description_result
            )
        except Exception as e:
            self.logger.error(f"调用工作流引擎失败: {e}", exc_info=True)
            self.display_area.append(f"\n-- 启动 AI 描述生成失败: {e} --")
            # 发生启动错误时也应该重新启用按钮
            if self._current_entity_id and not self.game_state_provider.find_entity(self._current_entity_id).is_destroyed:
                 self.generate_desc_button.setEnabled(True)


    # --- 这是工作流的回调方法 ---
    def _handle_ai_description_result(self, success: bool, result: Optional[Dict[str, Any]], error_message: Optional[str]):
        """处理从工作流引擎返回的 AI 描述结果。"""
        self.logger.debug(f"收到 AI 描述结果: success={success}, result={result}, error='{error_message}'")

        # 确保在正确的上下文中操作 (检查实体是否仍然是当前检查的那个)
        # 如果用户在等待期间检查了其他实体，我们不应更新显示
        # (简单起见，这里暂时不加这个检查，假设用户不会那么快切换)

        # 移除等待信息
        current_text = self.display_area.toPlainText()
        if current_text.endswith("\n-- 正在向 AI 请求描述... --"):
            self.display_area.setPlainText(current_text[:-len("\n-- 正在向 AI 请求描述... --")])

        if success and result:
            ai_description = result.get("description", "AI 未提供有效描述。") # 从结果字典获取描述
            self.display_area.append(f"\n-- AI 生成的描述 --\n{ai_description}\n--------------------")
            self.logger.info(f"成功获取并显示实体 '{self._current_entity_id}' 的 AI 描述。")
        else:
            err_msg = error_message or "未知错误"
            self.display_area.append(f"\n-- 获取 AI 描述失败: {err_msg} --")
            self.logger.error(f"获取实体 '{self._current_entity_id}' 的 AI 描述失败: {err_msg}")
            # 可以选择显示一个错误消息框
            # QMessageBox.warning(self, "AI 错误", f"无法生成描述:\n{err_msg}")

        # 无论成功失败，如果实体仍有效且未销毁，重新启用按钮
        if self._current_entity_id:
             try:
                 entity = self.game_state_provider.find_entity(self._current_entity_id)
                 if entity and not entity.is_destroyed:
                     self.generate_desc_button.setEnabled(True)
             except Exception as e:
                 self.logger.error(f"重新启用按钮时检查实体状态出错: {e}")


    @Slot()
    def _mark_entity_destroyed(self):
        """构造并提交 @Destroy 指令。"""
        if not self._current_entity_id or not self._current_entity_type:
            self.logger.warning("尝试销毁无效实体。")
            return

        self.logger.info(f"准备提交销毁指令给实体: {self._current_entity_id} ({self._current_entity_type})")

        # 构造指令字典
        destroy_command = {
            "command": "Destroy",
            "entity_type": self._current_entity_type,
            "entity_id": self._current_entity_id,
            "params": {} # Destroy 指令通常没有参数
        }

        try:
            self.command_submitter.submit_commands([destroy_command], source="EntityInspectorWidget")
            self.logger.info(f"销毁指令已提交给: {self._current_entity_id}")
            # 可以在这里添加提示，但状态最终会在 game_state_changed 后更新 UI
            self.display_area.append("\n-- 销毁指令已提交 --")
            # 指令提交后，禁用操作按钮，因为实体即将被销毁
            self.generate_desc_button.setEnabled(False)
            self.mark_destroyed_button.setEnabled(False)
        except Exception as e:
            self.logger.error(f"提交销毁指令失败: {e}", exc_info=True)
            QMessageBox.critical(self, "错误", f"提交销毁指令失败:\n{e}")


    # --- 公共方法 (可选) ---
    @Slot()
    def update_view(self):
        """
        当外部通知状态可能已改变时，重新加载当前检查的实体信息。
        会被 MainWindow 在 game_state_changed 时调用 (如果此视图当前可见)。
        """
        self.logger.debug(f"EntityInspectorWidget 接到更新通知，重新加载 {self._current_entity_id}")
        if self._current_entity_id:
            # 重新触发加载逻辑，会更新显示和按钮状态
            self._load_entity_info()


# --- 用于独立测试的 main ---
if __name__ == '__main__':
    import sys
    from PySide6.QtWidgets import QApplication
    from PySide6.QtCore import QObject, Signal
    # 导入真实的 Item 和 Place 类，以便正确模拟
    try:
        from core.world_state import Item, Place
    except ImportError:
         logging.critical("无法导入 core.world_state 中的 Item 和 Place，模拟测试无法正确运行。")
         sys.exit(1) # 无法进行有效测试，直接退出

    logging.basicConfig(level=logging.DEBUG)

    # --- 创建模拟服务 ---
    class MockGameState: # 模拟 WorldState 的一部分
        def __init__(self):
            # 1. 创建 Item 实例 (只传入核心字段)
            apple = Item(entity_id="apple-01")
            # 2. 设置动态属性
            apple.set_attribute("name", "红苹果")
            apple.set_attribute("description", "一个普通的红苹果。")
            apple.set_attribute("quantity", 1) # 假设有个数量

            # 1. 创建 Place 实例
            room = Place(entity_id="start-room")
            # 2. 设置动态属性
            room.set_attribute("name", "起始房间")
            room.set_attribute("contents", ["apple-01"]) # contents 是列表
            room.set_attribute("exits", {"north": "Place:corridor-01"}) # 假设有出口

            # 存储到模拟字典
            self.items = {"apple-01": apple}
            self.places = {"start-room": room}
            self.characters = {}
            self.focus = []

        def find_entity(self, entity_id, include_destroyed=False):
            entity = self.items.get(entity_id) or self.places.get(entity_id) or self.characters.get(entity_id)
            if entity and (not entity.is_destroyed or include_destroyed):
                return entity
            return None
        # MockGameState 不需要 get_all_attributes

    mock_world = MockGameState()

    class MockProvider(QObject, IGameStateProvider): # 继承 QObject 方便信号
        def find_entity(self, entity_id, include_destroyed=False):
            logging.debug(f"[MockProvider] find_entity: {entity_id}")
            return mock_world.find_entity(entity_id, include_destroyed)
        # 其他 IGameStateProvider 方法的简单实现...
        def get_world_state(self): return mock_world # 注意：返回的是 MockGameState，不是真的 WorldState
        def find_entity_by_name(self, name: str, entity_type = None, include_destroyed= False): return None
        def get_player_character_id(self): return None
        def get_player_character(self): return None
        def get_current_focus(self) -> List[str]: return []


    class MockSubmitter(QObject, ICommandSubmitter):
        # 定义信号
        commands_submitted_signal = Signal(list, str)
        game_state_updated_signal = Signal() # 模拟状态更新信号

        def submit_commands(self, commands: List[Dict[str, Any]], source: Optional[str] = None) -> None:
            logging.info(f"[MockSubmitter] 收到指令 from {source}: {commands}")
            self.commands_submitted_signal.emit(commands, source or "Unknown")
            # 模拟销毁
            for cmd in commands:
                if cmd["command"] == "Destroy":
                    entity = mock_world.find_entity(cmd["entity_id"])
                    if entity:
                        # 直接修改模拟对象的状态
                        entity.set_attribute("is_destroyed", True) # 使用 set_attribute
                        logging.info(f"[MockSubmitter] 模拟销毁: {cmd['entity_id']}")
            # 模拟状态更新完成
            from PySide6.QtCore import QTimer
            # 稍微增加延迟，确保销毁能在下一次 update_view 前生效
            QTimer.singleShot(200, self.game_state_updated_signal.emit)

    class MockEngine(QObject, IWorkflowEngine):
        def execute_workflow(self, config_id: str, context: Dict[str, Any], callback: WorkflowCallback) -> None:
            logging.info(f"[MockEngine] 执行工作流: {config_id}, context: {context}")
            from PySide6.QtCore import QTimer
            # 模拟异步延迟
            def delayed_callback():
                if config_id == "generate_description":
                    entity_id = context.get("entity_id")
                    # 查找实体以获取当前信息（如果需要的话）
                    entity = mock_world.find_entity(entity_id)
                    if entity:
                        desc = f"这是一个来自模拟 AI 的描述({entity_id}): {entity.get_attribute('name', '未知名称')}。"
                        callback(True, {"description": desc}, None)
                    elif entity_id == "apple-01": # 备用逻辑以防万一
                         callback(True, {"description": f"这是一个来自模拟 AI 的描述：这个苹果看起来格外诱人 ({entity_id})。"}, None)
                    else:
                        callback(False, None, f"模拟 AI 不知道如何描述 {entity_id}")
                else:
                    callback(False, None, f"未知的模拟工作流: {config_id}")
            QTimer.singleShot(1500, delayed_callback) # 模拟1.5秒延迟

        def load_workflow_configs(self, config_source) -> None: pass
        def get_workflow_config(self, config_id: str) -> Optional[Dict[str, Any]]: return None

    # --- 运行测试 ---
    app = QApplication(sys.argv)

    # 创建模拟服务实例
    provider = MockProvider()
    submitter = MockSubmitter()
    engine = MockEngine()

    # 创建窗口实例并注入服务
    window = EntityInspectorWidget(provider, submitter, engine)

    # 连接模拟信号进行测试
    submitter.commands_submitted_signal.connect(lambda cmds, src: print(f"测试捕获到指令提交: {cmds} from {src}"))
    # 连接状态更新信号到窗口的 update_view
    submitter.game_state_updated_signal.connect(window.update_view)

    window.setWindowTitle("实体检查器 (独立测试)")
    window.resize(500, 400)
    window.show()

    sys.exit(app.exec())