# main.py
import sys
import logging
from PySide6.QtWidgets import QApplication

# 导入核心后端组件 (假设它们可以被导入)
# from core.game_state import GameState, load_game
# from core.ai_service import AIService
# from core.prompts import get_system_prompt, clean_history_for_ai
# from core.parser import parse_commands
# from core.command_processor import CommandExecutor

# 导入 GUI 组件
from gui.main_window import MainWindow

# --- 配置 ---
LOG_LEVEL = logging.DEBUG
# DEEPSEEK_API_KEY = "YOUR_DEEPSEEK_API_KEY" # 强烈建议从环境变量或配置文件读取

class ApplicationController:
    """
    负责协调 GUI 和后端逻辑的主控制器。
    持有对 MainWindow, GameState, AIService 等的引用。
    """
    def __init__(self):
        self.logger = logging.getLogger(__name__)
        self.logger.info("初始化应用程序控制器...")

        # --- 1. 初始化后端服务 (占位) ---
        # self.game_state = self._initialize_game_state()
        # self.ai_service = self._initialize_ai_service()
        # self.command_executor = CommandExecutor() # 通常是静态方法，不需要实例

        # --- 2. 初始化 GUI ---
        self.app = QApplication(sys.argv)
        self.main_window = MainWindow()

        # --- 3. 连接 GUI 信号到控制器方法 ---
        self.main_window.narrative_view.user_input_submitted.connect(self.process_user_turn)
        # GUI 需要一个 game_state_changed 信号，连接到更新 UI 的方法
        # self.main_window.game_state_changed_signal.connect(self.main_window.update_ui_from_gamestate)

        self.logger.info("应用程序控制器初始化完成。")

    def _initialize_game_state(self):
        """初始化或加载游戏状态 (占位)"""
        # try:
        #     # 尝试加载存档
        #     game_state, conversation_log = load_game("saves/last_save.json")
        #     # TODO: 将 conversation_log 加载到某个地方
        #     self.logger.info("从存档加载游戏状态成功。")
        #     return game_state
        # except FileNotFoundError:
        #     self.logger.info("未找到存档文件，创建新的游戏状态。")
        #     return GameState()
        # except Exception as e:
        #     self.logger.error(f"加载游戏状态失败: {e}", exc_info=True)
        #     self.logger.warning("无法加载存档，将创建新的游戏状态。")
        #     return GameState()
        pass # 返回 None 或引发错误，直到实现

    def _initialize_ai_service(self):
        """初始化 AI 服务 (占位)"""
        # api_key = DEEPSEEK_API_KEY
        # if not api_key or api_key == "YOUR_DEEPSEEK_API_KEY":
        #     self.logger.error("未配置 DeepSeek API Key!")
        #     # 可以显示错误消息并退出，或返回 None
        #     # raise ValueError("请设置 DeepSeek API Key")
        #     return None
        # return AIService(api_key=api_key)
        pass # 返回 None 或引发错误，直到实现

    @Slot(str)
    def process_user_turn(self, user_input: str):
        """
        处理一个完整的用户回合：发送给 AI，处理响应和指令。
        这是核心的游戏循环驱动逻辑。
        """
        self.logger.info(f"开始处理用户回合: {user_input}")
        self.main_window.narrative_view.set_input_enabled(False)
        self.main_window.narrative_view.append_text(f"> {user_input}\n") # 显示用户输入

        # --- TODO: 实现完整的 AI 交互和状态更新 ---
        # 1. 准备数据:
        #    - 获取清理后的对话历史 (需要存储对话历史)
        #    - 生成系统提示 (prompts.get_system_prompt(self.game_state))
        # 2. 调用 AI:
        #    - response_stream = self.ai_service.get_completion_stream(system_prompt, history)
        # 3. 处理流式响应:
        #    - accumulated_response = ""
        #    - for chunk in response_stream:
        #        - self.main_window.narrative_view.append_text(chunk) # 实时显示
        #        - accumulated_response += chunk
        # 4. 处理完整响应:
        #    - 更新对话历史
        #    - 解析指令 (commands = parse_commands(accumulated_response))
        #    - 执行指令 (CommandExecutor.execute_commands(commands, self.game_state.world))
        # 5. 触发 UI 更新:
        #    - self.main_window.game_state_changed_signal.emit() # 发出信号
        # 6. 重新启用输入:
        #    - self.main_window.narrative_view.set_input_enabled(True)

        # --- 临时模拟逻辑 ---
        from PySide6.QtCore import QTimer
        def mock_ai_and_update():
            self.logger.debug("模拟 AI 响应和状态更新...")
            time.sleep(0.5) # 模拟 AI 思考
            ai_text = f"AI 收到了 '{user_input}'。世界没有变化（模拟）。\n"
            # 模拟指令执行 (这里不实际执行，只显示文本)
            # ai_text += "@Create Item bread-01 (name=\"一块面包\")\n"
            self.main_window.narrative_view.append_text(ai_text)

            # 模拟状态变化和 UI 更新
            # self.game_state.world.items["bread-01"] = Item(entity_id="bread-01", name="一块面包") # 伪代码
            # self.main_window.game_state_changed_signal.emit() # 触发更新
            self.main_window.update_ui_from_gamestate() # 直接调用更新 (简单场景)

            self.main_window.narrative_view.set_input_enabled(True)
            self.logger.info("用户回合处理完成 (模拟)。")

        # 使用 QTimer 延迟执行，给 GUI 时间先显示用户输入
        QTimer.singleShot(10, mock_ai_and_update)


    def run(self):
        """启动应用程序。"""
        self.logger.info("启动应用程序事件循环...")
        self.main_window.show()
        sys.exit(self.app.exec())

if __name__ == "__main__":
    # 配置日志
    logging.basicConfig(
        level=LOG_LEVEL,
        format='%(asctime)s - %(name)s - %(levelname)s - %(message)s',
        handlers=[
            logging.StreamHandler(sys.stdout) # 输出到控制台
            # logging.FileHandler("game.log", encoding='utf-8') # (可选) 输出到文件
        ]
    )
    # requests 和 urllib3 的日志级别通常设为 WARNING 或更高，避免过多输出
    logging.getLogger("requests").setLevel(logging.WARNING)
    logging.getLogger("urllib3").setLevel(logging.WARNING)

    # 创建并运行控制器
    controller = ApplicationController()
    # --- 后续需要将 game_state, ai_service 等实例传递给 controller 或在其内部创建 ---
    # 例如: controller.set_game_state(loaded_game_state)

    # 在启动前可以先进行一次 UI 更新，显示初始状态
    # controller.main_window.update_ui_from_gamestate()

    controller.run()