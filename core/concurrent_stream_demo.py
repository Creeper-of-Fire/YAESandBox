# core/concurrent_stream_demo.py
# 和主项目无关，用来展示AIService的多线程能力
import sys
import logging
import time
from typing import List, Optional, Iterator

from PySide6.QtCore import QObject, Signal, Slot, QThread, Qt
from PySide6.QtWidgets import (QApplication, QMainWindow, QWidget, QVBoxLayout,
                               QTextEdit, QPushButton, QPlainTextEdit)
from openai.types.chat.chat_completion_chunk import ChatCompletionChunk
from core.ai_service import AIService

# --- 配置 ---
LOG_LEVEL = logging.DEBUG
NUM_STREAMS = 2 # 同时启动的流数量
DEMO_PROMPT = "写一句关于编程的长诗，应该有800字。" # 用于所有流的简单提示

# --- 后台工作者 ---
class StreamWorker(QObject):
    text_received = Signal(int, str) # 修改信号
    finished = Signal(int)           # 让 finished 也带上索引，方便追踪
    error = Signal(int, str)         # 让 error 也带上索引

    # 添加 worker_index
    def __init__(self, worker_index: int, ai_service: AIService, prompt: str, history: Optional[List] = None, parent=None):
        super().__init__(parent)
        self.worker_index = worker_index # 存储索引
        self.ai_service = ai_service
        self.prompt = prompt
        self.history = history if history else []
        self._is_running = True

    @Slot()
    def run(self):
        if not self.ai_service or not self.ai_service.client:
            self.error.emit(self.worker_index, "AI Service 未初始化。")
            self.finished.emit(self.worker_index)
            return

        logging.info(f"Worker {self.worker_index} (ID: {id(self)}): 开始请求 AI 流...")
        try:
            system_prompt = "你是一个乐于助人的助手。"
            message_history = self.history + [{"role": "user", "content": self.prompt}]
            stream: Optional[Iterator[ChatCompletionChunk]] = self.ai_service.get_completion_stream(system_prompt, message_history)

            if stream is None:
                 self.error.emit(self.worker_index, "无法获取 AI 响应流。")
                 self.finished.emit(self.worker_index)
                 return

            chars_accumulated = ""  # 用于累积字符
            last_emit_time = time.time()
            BUFFER_TIME = 0.1  # 每 0.1 秒最多发送一次信号 (可调整)

            for chunk in stream:
                if not self._is_running:
                    logging.info(f"Worker {self.worker_index} (ID: {id(self)}): 被外部停止。")
                    break
                if chunk.choices and chunk.choices[0].delta and chunk.choices[0].delta.content:
                    text_piece = chunk.choices[0].delta.content
                    chars_accumulated += text_piece
                    current_time = time.time()

                    # 减少信号频率：累积一定时间或缓冲区有内容时发送
                    if chars_accumulated and (current_time - last_emit_time >= BUFFER_TIME):
                        self.text_received.emit(self.worker_index, chars_accumulated)
                        chars_accumulated = ""  # 清空缓冲区
                        last_emit_time = current_time

            # 发送缓冲区中剩余的最后部分 (重要!)
            if chars_accumulated:
                self.text_received.emit(self.worker_index, chars_accumulated)
        except Exception as e:
            logging.error(f"Worker {self.worker_index} (ID: {id(self)}): 发生错误: {e}", exc_info=True)
            self.error.emit(self.worker_index, f"错误: {e}")
        finally:
            logging.info(f"Worker {self.worker_index} (ID: {id(self)}): 流处理完成。")
            self.finished.emit(self.worker_index) # 发送完成信号和索引

    def stop(self):
         self._is_running = False

# --- 主窗口 ---
class MainWindow(QMainWindow):
    def __init__(self, parent=None):
        super().__init__(parent)
        self.setWindowTitle("并发 AI 流式处理演示")
        self.setGeometry(100, 100, 800, 600)

        # 初始化共享的 AI 服务
        self.ai_service = AIService() # 使用 ai_service.py 中的配置

        # 存储 worker 和 thread 的引用，防止被垃圾回收
        self.workers: List[StreamWorker] = []
        self.threads: List[QThread] = []

        self._init_ui()

    def _init_ui(self):
        """初始化界面"""
        central_widget = QWidget()
        self.setCentralWidget(central_widget)
        layout = QVBoxLayout(central_widget)

        self.start_button = QPushButton(f"同时启动 {NUM_STREAMS} 个流")
        self.start_button.clicked.connect(self.start_all_streams)
        layout.addWidget(self.start_button)

        self.text_edits: List[QPlainTextEdit] = []
        for i in range(NUM_STREAMS):
            text_edit = QPlainTextEdit()
            text_edit.setReadOnly(True)
            text_edit.setPlaceholderText(f"流 {i+1} 的输出...")
            layout.addWidget(text_edit)
            self.text_edits.append(text_edit)

    @Slot()
    def start_all_streams(self):
        """启动所有后台流式任务"""
        if not self.ai_service or not self.ai_service.client:
             logging.error("无法启动流：AI Service 未正确初始化。请检查 API Key。")
             # 可以添加一个 QMessageBox 提示用户
             return

        self.start_button.setEnabled(False) # 防止重复点击
        self.clear_all_displays()
        # 清理旧的 worker 和 thread (如果再次运行)
        self._cleanup_threads()

        logging.info(f"准备启动 {NUM_STREAMS} 个流...")

        self.active_threads = set(range(NUM_STREAMS))  # 跟踪活动的线程索引

        for i in range(NUM_STREAMS):
            thread = QThread()
            prompt = f"写一首关于数字 {i + 1} 的800字长诗。"  # 长提示
            # 传入索引 i
            worker = StreamWorker(i, self.ai_service, prompt)
            worker.moveToThread(thread)

            # --- 修改信号连接 ---
            worker.text_received.connect(self.handle_text_received)
            worker.error.connect(self.handle_error)
            worker.finished.connect(self.handle_finished)  # 连接新的 finished 槽
            # ---------------------

            thread.started.connect(worker.run)
            # 不要在这里连接 worker.finished 到 thread.quit
            # 让 handle_finished 决定何时退出线程

            # --- 修改自动删除逻辑 ---
            # 不在这里连接 deleteLater，移到 handle_finished 中处理
            # worker.finished.connect(worker.deleteLater)
            # thread.finished.connect(thread.deleteLater)
            # -----------------------

            thread.start()
            self.threads.append(thread)
            self.workers.append(worker)
            logging.info(f"已启动流 {i + 1} 的后台线程。")

    def clear_all_displays(self):
         """清空所有文本区域"""
         for text_edit in self.text_edits:
             text_edit.clear()

    def _cleanup_threads(self):
         """停止并清理之前的线程和工作者"""
         logging.debug("清理旧的线程和工作者...")
         for worker in self.workers:
             worker.stop() # 尝试优雅停止
         # 等待一小段时间让线程退出 (不是最佳方式，但对于demo可行)
         # time.sleep(0.1)
         # for thread in self.threads:
         #     if thread.isRunning():
         #         thread.quit()
         #         thread.wait(100) # 等待最多100ms
         #     # deleteLater 已经在 finished 信号中连接
         self.workers.clear()
         self.threads.clear()

         # --- 添加新的槽函数 ---

    @Slot(int, str)
    def handle_text_received(self, index: int, text: str):
        """安全地处理接收到的文本块"""
        if 0 <= index < len(self.text_edits):
            target_edit = self.text_edits[index]
            # 可以加一层更健壮的检查，确保 target_edit 仍然有效
            # 例如，检查它是否仍然是 central_widget 的子控件等
            # if target_edit and target_edit.parentWidget():
            try:
                target_edit.insertPlainText(text)
                target_edit.ensureCursorVisible()  # 自动滚动到底部
            except RuntimeError as e:
                # 如果对象已被删除，这里会捕获异常
                logging.warning(f"尝试更新索引 {index} 的 QTextEdit 时出错（可能已被删除）: {e}")
        else:
            logging.error(f"收到无效索引 {index} 的文本。")

    @Slot(int, str)
    def handle_error(self, index: int, error_message: str):
        """处理来自 Worker 的错误信息"""
        logging.error(f"流 {index + 1} 发生错误: {error_message}")
        if 0 <= index < len(self.text_edits):
            target_edit = self.text_edits[index]
            try:
                target_edit.append(f"<font color='red'>错误: {error_message}</font>")
            except RuntimeError as e:
                logging.warning(f"尝试向索引 {index} 的 QTextEdit 追加错误信息时出错: {e}")

    @Slot(int)
    def handle_finished(self, index: int):
        """处理 Worker 完成的信号"""
        logging.info(f"流 {index + 1} (Worker {index}) 完成。")
        self.active_threads.discard(index)  # 从活动集合中移除

        # 找到对应的线程和 Worker (如果需要)
        # 这里需要一种方法将索引映射回 thread/worker 对象，或者直接操作它们
        # 简单的实现：假设 self.threads 和 self.workers 的索引与 i 一致
        if 0 <= index < len(self.threads) and self.threads[index]:
            thread = self.threads[index]
            if thread.isRunning():
                thread.quit()
                # 可以考虑 thread.wait() 如果需要确保完全退出
            # 安全地安排删除
            if self.workers[index]:
                self.workers[index].deleteLater()
                self.workers[index] = None  # 清除引用
            thread.deleteLater()
            self.threads[index] = None  # 清除引用

        # 检查是否所有线程都完成了
        if not self.active_threads:
            logging.info("所有流处理完成。")
            self.start_button.setEnabled(True)  # 重新启用按钮

    def closeEvent(self, event):
         """关闭窗口时尝试清理线程"""
         self._cleanup_threads()
         super().closeEvent(event)

# --- 程序入口 ---
if __name__ == "__main__":
    logging.basicConfig(
        level=logging.DEBUG, # <--- 确保是 DEBUG
        format='%(asctime)s - %(threadName)s - %(name)s - %(levelname)s - %(message)s',
        handlers=[logging.StreamHandler(sys.stdout)]
    )
    # 屏蔽 requests 和 urllib3 的 DEBUG 日志，除非需要调试网络层
    logging.getLogger("requests").setLevel(logging.WARNING)
    logging.getLogger("urllib3").setLevel(logging.WARNING)
    logging.getLogger("openai").setLevel(logging.INFO) # openai 库日志级别


    app = QApplication(sys.argv)
    main_win = MainWindow()
    main_win.show()
    sys.exit(app.exec())