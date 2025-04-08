# gui/main_window.py
import logging
import sys
from PySide6.QtWidgets import (QMainWindow, QWidget, QVBoxLayout, QHBoxLayout,
                             QStackedWidget, QApplication, QLabel, QDockWidget)
from PySide6.QtCore import Qt, Slot

# 导入我们刚创建的叙事视图
from .narrative_widget import NarrativeWidget
# 导入未来可能需要的其他视图 (占位)
# from .backpack_widget import BackpackWidget
# from .map_widget import MapWidget

class MainWindow(QMainWindow):
    """
    游戏主窗口，负责整体布局和视图切换。
    """
    def __init__(self, parent=None):
        """
        初始化主窗口。
        """
        super().__init__(parent)
        self.logger = logging.getLogger(__name__)
        self.setWindowTitle("AI 驱动文本 RPG")
        self.setGeometry(100, 100, 1200, 800) # 设置初始位置和大小

        self._init_central_widget()
        self._init_docks() # 初始化左右侧栏
        self._init_views()
        self._connect_signals()

        self.logger.info("主窗口初始化完毕。")

    def _init_central_widget(self):
        """
        初始化中央部件，放置 QStackedWidget。
        """
        # 创建 QStackedWidget 用于管理不同的视图
        self.view_stack = QStackedWidget(self)
        self.setCentralWidget(self.view_stack)
        self.logger.debug("中央 QStackedWidget 初始化完毕。")

    def _init_docks(self):
        """
        初始化左右侧边栏 (使用 QDockWidget)。
        """
        # --- 左侧栏 (例如：状态、地图出口) ---
        self.left_dock = QDockWidget("状态/导航", self)
        self.left_dock.setAllowedAreas(Qt.DockWidgetArea.LeftDockWidgetArea | Qt.DockWidgetArea.RightDockWidgetArea)
        # 创建一个简单的占位符部件
        left_widget = QLabel("左侧栏内容 (如角色状态、地图出口)")
        left_widget.setAlignment(Qt.AlignmentFlag.AlignCenter)
        left_widget.setWordWrap(True)
        self.left_dock.setWidget(left_widget)
        self.addDockWidget(Qt.DockWidgetArea.LeftDockWidgetArea, self.left_dock)
        self.logger.debug("左侧 QDockWidget 初始化完毕。")

        # --- 右侧栏 (例如：背包快捷栏、焦点实体) ---
        self.right_dock = QDockWidget("辅助信息", self)
        self.right_dock.setAllowedAreas(Qt.DockWidgetArea.LeftDockWidgetArea | Qt.DockWidgetArea.RightDockWidgetArea)
        # 创建一个简单的占位符部件
        right_widget = QLabel("右侧栏内容 (如背包预览、焦点实体)")
        right_widget.setAlignment(Qt.AlignmentFlag.AlignCenter)
        right_widget.setWordWrap(True)
        self.right_dock.setWidget(right_widget)
        self.addDockWidget(Qt.DockWidgetArea.RightDockWidgetArea, self.right_dock)
        self.logger.debug("右侧 QDockWidget 初始化完毕。")

        # 可以添加菜单项来控制 Dock 的显示/隐藏
        view_menu = self.menuBar().addMenu("视图")
        view_menu.addAction(self.left_dock.toggleViewAction())
        view_menu.addAction(self.right_dock.toggleViewAction())

    def _init_views(self):
        """
        初始化并添加所有视图到 QStackedWidget。
        """
        # 1. 创建叙事视图实例
        self.narrative_view = NarrativeWidget(self)
        self.view_stack.addWidget(self.narrative_view)
        # 可以记录索引或使用对象本身查找
        self.logger.debug("叙事视图已添加到 StackedWidget。")

        # 2. 创建其他视图实例 (占位)
        # self.backpack_view = BackpackWidget(self)
        # self.view_stack.addWidget(self.backpack_view)
        # self.map_view = MapWidget(self)
        # self.view_stack.addWidget(self.map_view)
        # ... 添加更多视图 ...

        # 设置默认显示的视图 (叙事视图)
        self.view_stack.setCurrentWidget(self.narrative_view)
        self.logger.info("默认视图设置为叙事视图。")


    def _connect_signals(self):
        """
        连接主窗口级别的信号和槽。
        主要是连接视图发出的信号到处理函数。
        """
        # 连接叙事视图的 user_input_submitted 信号到主窗口的处理方法
        # self.narrative_view.user_input_submitted.connect(self.handle_user_input)
        self.logger.debug("主窗口信号连接完毕。")


    # --- 视图切换方法 (示例) ---
    @Slot()
    def show_narrative_view(self):
        """切换到叙事视图"""
        self.view_stack.setCurrentWidget(self.narrative_view)
        self.narrative_view.set_input_enabled(True) # 确保输入是启用的
        self.logger.info("切换到叙事视图。")

    # @Slot()
    # def show_backpack_view(self):
    #     """切换到背包视图"""
    #     if hasattr(self, 'backpack_view'):
    #         self.view_stack.setCurrentWidget(self.backpack_view)
    #         self.narrative_view.set_input_enabled(False) # 背包视图通常禁用主输入
    #         self.logger.info("切换到背包视图。")
    #     else:
    #         self.logger.warning("尝试切换到不存在的背包视图。")

    # --- 其他辅助方法 ---
    def update_ui_from_gamestate(self):
        """
        **核心方法**: 根据当前的 GameState 更新整个 UI。
        会被 game_state_changed 信号触发。
        """
        self.logger.info("正在根据 GameState 更新 UI...")
        # --- TODO: 实现 UI 更新逻辑 ---
        # 1. 更新左侧栏 (例如: 当前地点出口, 角色状态摘要)
        #    - 获取 GameState.world.find_entity(player_id) 的 current_place
        #    - 获取该地点的 exits
        #    - 更新 left_dock 中的部件
        # 2. 更新右侧栏 (例如: 焦点实体列表, 背包预览)
        #    - 获取 GameState.user_focus
        #    - 获取焦点实体的信息
        #    - 更新 right_dock 中的部件
        # 3. (可选) 如果当前视图需要更新 (例如背包视图)，调用其特定更新方法
        #    current_widget = self.view_stack.currentWidget()
        #    if isinstance(current_widget, BackpackWidget):
        #        current_widget.update_display(self.game_state) # 假设 BackpackWidget 有此方法

        # 临时占位符更新
        # left_content = "左侧栏: 待更新..."
        # player_char = self.game_state.find_entity_by_name("玩家") # 假设玩家叫 "玩家"
        # if player_char and player_char.get_attribute('current_place'):
        #     place_id = player_char.get_attribute('current_place')[1] # 假设是元组
        #     current_place = self.game_state.find_entity(place_id)
        #     if current_place:
        #         exits = current_place.get_attribute('exits', {})
        #         exit_text = "\n".join([f"- {direction}: {target_id}" for direction, target_id in exits.items()])
        #         left_content = f"当前位置: {current_place.get_attribute('name', place_id)}\n出口:\n{exit_text}"

        # self.left_dock.widget().setText(left_content) # 更新左侧栏标签内容 (仅适用于 QLabel)

        self.logger.info("UI 更新完成 (占位逻辑)。")


# --- 应用程序入口点 ---
def main():
    """应用程序主入口函数"""
    logging.basicConfig(level=logging.DEBUG, format='%(asctime)s - %(name)s - %(levelname)s - %(message)s')
    app = QApplication(sys.argv)

    main_win = MainWindow()

    # --- TODO: 初始化 GameState, AI Service 等后端实例 ---
    # from core.game_state import GameState
    # from core.ai_service import AIService
    # game_state = GameState()
    # ai_service = AIService(api_key="YOUR_DEEPSEEK_API_KEY") # 从配置或环境变量获取
    # main_win.set_backend(game_state, ai_service) # 需要在 MainWindow 添加此方法

    main_win.show()
    sys.exit(app.exec())

# main 函数不是必需的，可以将入口逻辑放在顶层 main.py
# 但放在这里便于单独测试 MainWindow
# if __name__ == '__main__':
#     main()