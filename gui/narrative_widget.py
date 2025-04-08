# gui/narrative_widget.py
import logging

from PySide6.QtCore import Signal, Slot, Qt, QEvent  # 导入 Qt, QEvent
from PySide6.QtGui import QKeyEvent  # 导入 QKeyEvent, QKeySequence
from PySide6.QtWidgets import (QWidget, QVBoxLayout, QTextEdit, QPushButton,
                               QHBoxLayout, QSplitter, QSizePolicy, QSpacerItem)  # 导入 QSplitter, QSizePolicy, QSpacerItem


class NarrativeWidget(QWidget):
    """
    叙事视图部件，使用 QSplitter 分隔显示区和可调整大小的多行输入区。
    """
    # 定义信号，当用户发送输入时发出，传递输入的文本
    user_input_submitted = Signal(str)

    def __init__(self, parent=None):
        """
        初始化叙事视图。
        """
        super().__init__(parent)
        self.logger = logging.getLogger(__name__)
        self._init_ui()
        self._connect_signals()

    def _init_ui(self):
        """
        初始化用户界面元素，使用 QSplitter，并调整初始比例。
        """
        main_layout = QVBoxLayout(self)
        main_layout.setContentsMargins(0, 0, 0, 0)

        splitter = QSplitter(Qt.Orientation.Vertical, self)

        # --- 上方：显示区域 ---
        self.display_area = QTextEdit(splitter)
        self.display_area.setReadOnly(True)
        self.display_area.setPlaceholderText("游戏故事将在这里展开...")
        # 让显示区域在垂直方向上优先扩展
        self.display_area.setSizePolicy(QSizePolicy.Policy.Expanding, QSizePolicy.Policy.Expanding)

        # --- 下方：输入区域容器 ---
        input_container = QWidget(splitter)
        input_container_layout = QVBoxLayout(input_container)
        input_container_layout.setContentsMargins(5, 5, 5, 5)
        input_container_layout.setSpacing(5)

        # 输入框: QTextEdit
        self.input_line = QTextEdit(input_container)
        self.input_line.setPlaceholderText("输入你的行动... (Ctrl+Enter 发送)")
        self.input_line.setSizePolicy(QSizePolicy.Policy.Expanding, QSizePolicy.Policy.Expanding)
        self.input_line.setMinimumHeight(40)  # 保留最小高度
        # self.input_line.setMaximumHeight(300) # 可以根据需要取消注释或调整

        self.input_line.setAcceptRichText(False)
        self.input_line.setTabChangesFocus(False)

        input_container_layout.addWidget(self.input_line, 1)  # 仍然让它在容器内扩展

        # 发送按钮行
        button_layout = QHBoxLayout()
        spacer = QSpacerItem(40, 20, QSizePolicy.Policy.Expanding, QSizePolicy.Policy.Minimum)
        button_layout.addSpacerItem(spacer)
        self.send_button = QPushButton("发送 (Ctrl+Enter)", input_container)
        button_layout.addWidget(self.send_button)
        input_container_layout.addLayout(button_layout)

        # --- 将 Splitter 添加到主布局 ---
        main_layout.addWidget(splitter)

        # --- 使用 setStretchFactor 设置初始比例 ---
        splitter.setStretchFactor(0, 8)  # 显示区比例
        splitter.setStretchFactor(1, 2)  # 输入区比例

        self.logger.debug("叙事视图 UI 初始化完毕 (使用 QSplitter, 调整比例)。")

        self.input_line.installEventFilter(self)


    def _connect_signals(self):
        """
        连接内部信号和槽。
        主要连接发送按钮。
        """
        self.send_button.clicked.connect(self._on_send_clicked)
        # QTextEdit 没有 returnPressed 信号，我们用事件过滤器处理 Ctrl+Enter
        self.logger.debug("叙事视图信号连接完毕。")


    @Slot()
    def _on_send_clicked(self):
        """
        处理发送按钮点击事件。
        """
        user_text = self.input_line.toPlainText().strip()  # 获取纯文本并去除首尾空白
        if user_text:
            self.logger.info(f"用户输入: {user_text}")
            self.user_input_submitted.emit(user_text)
            self.input_line.clear()  # 清空输入框
        else:
            self.logger.debug("用户尝试发送空输入，已忽略。")


    # --- 事件过滤器处理 Ctrl+Enter ---
    def eventFilter(self, watched, event):
        """
        事件过滤器，用于监听输入框的键盘事件。
        """
        if watched == self.input_line and event.type() == QEvent.Type.KeyPress:
            key_event = QKeyEvent(event)  # 强制转换类型以便获取 key 和 modifiers
            # 检查是否按下了 Enter 键 (Qt.Key_Return 或 Qt.Key_Enter)
            # 并且是否按下了 Ctrl 修饰键 (Qt.ControlModifier)
            is_enter = key_event.key() == Qt.Key.Key_Return or key_event.key() == Qt.Key.Key_Enter
            has_ctrl = key_event.modifiers() & Qt.KeyboardModifier.ControlModifier

            if is_enter and has_ctrl:
                self.logger.debug("检测到 Ctrl+Enter，触发发送。")
                self._on_send_clicked()  # 调用发送逻辑
                return True  # 事件已处理，不再传递

        # 对于其他事件，返回默认处理
        return super().eventFilter(watched, event)


    # --- 公共方法 ---
    @Slot(str)
    def append_text(self, text: str):
        self.display_area.append(text)
        self.display_area.ensureCursorVisible()
        # self.logger.debug(f"向叙事视图追加文本: {text[:50]}...") # 日志有点多，暂时注释


    @Slot()
    def clear_display(self):
        self.display_area.clear()
        self.logger.info("叙事视图显示区域已清空。")


    def set_input_enabled(self, enabled: bool):
        self.input_line.setEnabled(enabled)
        self.send_button.setEnabled(enabled)
        if not enabled:
            # 保留占位符文本，仅视觉上禁用
            pass
            # self.input_line.setPlaceholderText("等待 AI 响应...") # 禁用时修改占位符意义不大
        else:
            pass
            # self.input_line.setPlaceholderText("输入你的行动... (Ctrl+Enter 发送)")
        self.logger.debug(f"叙事视图输入已 {'启用' if enabled else '禁用'}.")


# --- 可选的测试代码 ---
if __name__ == '__main__':
    import sys
    from PySide6.QtWidgets import QApplication

    logging.basicConfig(level=logging.DEBUG)

    app = QApplication(sys.argv)
    widget = NarrativeWidget()

    # 测试信号连接
    widget.user_input_submitted.connect(lambda text: widget.append_text(f"信号捕获: {text}\n"))

    widget.show()
    widget.resize(800, 600)  # 给窗口一个初始大小以便测试 Splitter
    sys.exit(app.exec())
