# gui/narrative_widget.py
import logging

from PySide6.QtCore import Signal, Slot
from PySide6.QtWidgets import (QWidget, QVBoxLayout, QTextEdit, QLineEdit, QPushButton, QHBoxLayout)


class NarrativeWidget(QWidget):
    """
    叙事视图部件，用于显示游戏故事文本和接收用户输入。
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
        初始化用户界面元素。
        """
        layout = QVBoxLayout(self)
        layout.setContentsMargins(0, 0, 0, 0)  # 通常子部件不需要外边距

        # --- 显示区域 ---
        self.display_area = QTextEdit(self)
        self.display_area.setReadOnly(True)  # 只读
        self.display_area.setPlaceholderText("游戏故事将在这里展开...")
        layout.addWidget(self.display_area, 1)  # 占据更多垂直空间

        # --- 输入区域 ---
        input_layout = QHBoxLayout()  # 水平布局放输入框和按钮

        self.input_line = QLineEdit(self)
        self.input_line.setPlaceholderText("输入你的行动...")
        input_layout.addWidget(self.input_line, 1)  # 输入框占据更多水平空间

        self.send_button = QPushButton("发送", self)
        input_layout.addWidget(self.send_button)

        # 将输入区域布局添加到主垂直布局
        layout.addLayout(input_layout)

        self.logger.debug("叙事视图 UI 初始化完毕。")

    def _connect_signals(self):
        """
        连接内部信号和槽。
        """
        # 点击发送按钮 或 在输入框按回车 时，触发 _on_send_clicked 槽函数
        self.send_button.clicked.connect(self._on_send_clicked)
        self.input_line.returnPressed.connect(self._on_send_clicked)
        self.logger.debug("叙事视图信号连接完毕。")

    @Slot()
    def _on_send_clicked(self):
        """
        处理发送按钮点击或回车事件。
        """
        user_text = self.input_line.text().strip()  # 获取并去除首尾空白
        if user_text:
            self.logger.info(f"用户输入: {user_text}")
            # --- 核心逻辑: 发出信号 ---
            # 发出信号，将用户输入的文本传递出去
            self.user_input_submitted.emit(user_text)

            # --- 临时回显 (用于快速验证，后续会被后端驱动的显示替代) ---
            # self.append_text(f"> {user_text}\n") # 暂时注释掉，显示应由主逻辑控制

            # 清空输入框
            self.input_line.clear()
        else:
            self.logger.debug("用户尝试发送空输入，已忽略。")

    # --- 公共方法 ---
    @Slot(str)
    def append_text(self, text: str):
        """
        向显示区域追加文本。确保在主线程调用。
        添加换行符以分隔不同块的文本。
        """
        # 自动滚动到底部
        self.display_area.append(text)  # append 会自动处理换行
        # 可以考虑添加时间戳或其他格式化
        self.display_area.ensureCursorVisible()  # 滚动到底部
        self.logger.debug(f"向叙事视图追加文本: {text[:50]}...")  # 日志记录部分内容

    @Slot()
    def clear_display(self):
        """
        清空显示区域。
        """
        self.display_area.clear()
        self.logger.info("叙事视图显示区域已清空。")

    def set_input_enabled(self, enabled: bool):
        """
        启用或禁用用户输入。
        """
        self.input_line.setEnabled(enabled)
        self.send_button.setEnabled(enabled)
        if not enabled:
            self.input_line.setPlaceholderText("等待 AI 响应...")
        else:
            self.input_line.setPlaceholderText("输入你的行动...")
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
    sys.exit(app.exec())
