# concurrent_stream_demo.py
import sys
import logging
import threading
import time
from typing import List, Optional, Iterator, Union, Any, Dict

from PySide6.QtCore import QObject, Signal, Slot, QThread, Qt, QCoreApplication, QMutexLocker, QMutex, QThreadPool, QRunnable
from PySide6.QtWidgets import (QApplication, QMainWindow, QWidget, QVBoxLayout,
                             QPlainTextEdit, QPushButton, QMessageBox, QComboBox, QLabel, QHBoxLayout) # 添加 QComboBox, QLabel, QHBoxLayout

# --- Google GenAI Imports ---
try:
    import google.generativeai as genai
    # Google 返回的具体类型
    from google.generativeai.types import GenerateContentResponse
    # Google 可能的异常
    from google.api_core.exceptions import GoogleAPICallError, RetryError
    GOOGLE_AVAILABLE = True
except ImportError:
    logging.warning("无法导入 google.generativeai。Google Gemini 功能将不可用。请运行 'pip install google-generativeai'")
    GOOGLE_AVAILABLE = False
    # 定义占位符类型以便代码能运行
    class GenerateContentResponse: pass
    class GoogleAPICallError(Exception): pass
    class RetryError(GoogleAPICallError): pass


# --- OpenAI/DeepSeek Imports ---
try:
    from openai import OpenAI, APITimeoutError, APIConnectionError, RateLimitError, APIStatusError
    from openai.types.chat.chat_completion_chunk import ChatCompletionChunk
    OPENAI_AVAILABLE = True
except ImportError:
    logging.warning("无法导入 openai。DeepSeek 功能将不可用。请运行 'pip install openai'")
    OPENAI_AVAILABLE = False
    # 定义占位符类型以便代码能运行
    class ChatCompletionChunk: pass
    class APITimeoutError(Exception): pass
    class APIConnectionError(Exception): pass
    class RateLimitError(Exception): pass
    class APIStatusError(Exception): pass


# --- 配置 ---
LOG_LEVEL = logging.DEBUG
NUM_STREAMS = 2 # 保持为 2 进行测试
DEMO_PROMPT_TEMPLATE = "写一首关于数字 {index} 的800字长诗。" # 模板

# --- Google AI Service ---
if GOOGLE_AVAILABLE:
    GOOGLE_API_KEY = "AIzaSyDONJBrkpRsbhv3ehDLjcp0A2jZbE3tb2o" # 使用你提供的临时 Key
    GOOGLE_MODEL_NAME = "gemini-2.0-flash" # 使用一个常见的模型，确认是否可用

    class GoogleAIService:
        """封装与 Google Gemini API 的通信服务。"""
        def __init__(self, api_key: str = GOOGLE_API_KEY, model: str = GOOGLE_MODEL_NAME):
            self.model_name = model
            self.client: Optional[genai.GenerativeModel] = None
            try:
                # 配置 API Key
                genai.configure(api_key=api_key)
                # 创建模型实例
                self.client = genai.GenerativeModel(self.model_name)
                logging.info(f"Google GenAI 客户端初始化成功。模型: {model}")
            except Exception as e:
                logging.error(f"初始化 Google GenAI 客户端时出错: {e}")

        def get_completion_stream(self, user_prompt: str, messages_history: Optional[List] = None) -> Optional[Iterator[GenerateContentResponse]]:
            """
            向 Google Gemini 发送请求并获取流式响应。
            简化版：仅使用当前 user_prompt。
            """
            if not self.client:
                logging.error("无法获取 Google AI 响应：客户端未初始化。")
                return None

            logging.info(f"准备向 Google AI 发送请求。模型: {self.model_name}")
            # logging.debug(f"请求内容: {user_prompt}") # Debug

            try:
                # 直接使用 prompt 列表调用 generate_content_stream
                response_stream = self.client.generate_content(
                    contents=[user_prompt], # 最简单的形式
                    stream=True
                )
                logging.info("已成功发送请求到 Google AI，开始接收流式响应...")
                return response_stream

            except (GoogleAPICallError, RetryError) as e:
                 logging.error(f"调用 Google AI API 时出错: {e}")
                 raise # 原型阶段让其崩溃
            except Exception as e:
                logging.error(f"调用 Google AI API 时发生未预料的错误: {e}", exc_info=True)
                raise

# --- DeepSeek AI Service ---
if OPENAI_AVAILABLE:
    # DeepSeek 配置保持不变
    DEEPSEEK_API_KEY = "sk-ee30faa083f94e71837a36cf5f870eab"
    DEEPSEEK_BASE_URL = "https://api.deepseek.com"
    DEEPSEEK_MODEL_NAME = "deepseek-chat"

    class AIService: # 重命名为 DeepSeekAIService 以示区分
        """封装与 AI 模型 (DeepSeek) 的通信服务。"""
        def __init__(self, api_key: Optional[str] = None, base_url: str = DEEPSEEK_BASE_URL, model: str = DEEPSEEK_MODEL_NAME):
            resolved_api_key = api_key if api_key else DEEPSEEK_API_KEY
            self.model_name = model
            self.client: Optional[OpenAI] = None
            if not resolved_api_key or "sk-xxx" in resolved_api_key:
                logging.error("DeepSeek AI 服务初始化失败：未提供有效的 API Key。")
            else:
                try:
                    self.client = OpenAI(api_key=resolved_api_key, base_url=base_url)
                    logging.info(f"DeepSeek (OpenAI Client) 初始化成功。模型: {model}, Base URL: {base_url}")
                except Exception as e:
                    logging.error(f"初始化 DeepSeek (OpenAI Client) 时出错: {e}")

        def get_completion_stream(self, system_prompt: str, messages_history: List[Dict[str, str]]) -> Optional[Iterator[ChatCompletionChunk]]:
            if not self.client:
                logging.error("无法获取 DeepSeek AI 响应：AI 客户端未初始化。")
                return None
            request_messages = [{"role": "system", "content": system_prompt}] + messages_history
            logging.info(f"准备向 DeepSeek AI 发送 {len(request_messages)} 条消息。模型: {self.model_name}")
            try:
                response_stream = self.client.chat.completions.create(
                    model=self.model_name,
                    messages=request_messages,
                    stream=True,
                )
                logging.info("已成功发送请求到 DeepSeek AI，开始接收流式响应...")
                return response_stream
            except (APITimeoutError, APIConnectionError, RateLimitError, APIStatusError) as e:
                logging.error(f"调用 DeepSeek AI API 时出错: {e}")
                raise
            except Exception as e:
                logging.error(f"调用 DeepSeek AI API 时发生未预料的错误: {e}", exc_info=True)
                raise

# --- 信号载体 ---
class WorkerSignals(QObject):
    """
    定义从 QRunnable 任务发出的信号。
    必须是 QObject 才能定义信号。
    """
    text_received = Signal(int, str)
    finished = Signal(int)
    error = Signal(int, str)

# --- Base Runnable ---
class BaseRunnable(QRunnable):
    """
    QRunnable 的基类，持有通用属性和信号载体。
    """
    def __init__(self, worker_index: int):
        super().__init__()
        self.worker_index = worker_index
        self.signals = WorkerSignals() # 每个任务实例拥有自己的信号载体
        self._is_running = True # 控制循环的标志

    # QRunnable 没有 stop 槽，通过外部设置标志位
    def request_stop(self):
        logging.debug(f"Runnable {self.worker_index}: Received stop request.")
        self._is_running = False

    @Slot()
    def run(self):
        raise NotImplementedError("子类必须实现 run 方法")

# --- DeepSeek Runnable ---
if OPENAI_AVAILABLE:
    class DeepSeekStreamRunnable(BaseRunnable):
        def __init__(self, worker_index: int, ai_service: AIService, prompt: str, history: Optional[List] = None):
            super().__init__(worker_index)
            self.ai_service = ai_service
            self.prompt = prompt
            self.history = history if history else []

        @Slot()
        def run(self):
            thread_id = threading.get_native_id() # 获取当前线程ID
            logging.info(f"DeepSeekRunnable {self.worker_index} (ID: {id(self)}) started in thread {thread_id}")
            if not self.ai_service or not self.ai_service.client:
                self.signals.error.emit(self.worker_index, "DeepSeek AI Service 未初始化。")
                self.signals.finished.emit(self.worker_index)
                return
            try:
                system_prompt = "你是一个乐于助人的助手。"
                message_history = self.history + [{"role": "user", "content": self.prompt}]
                stream: Optional[Iterator[ChatCompletionChunk]] = self.ai_service.get_completion_stream(system_prompt, message_history)
                if stream is None:
                     self.signals.error.emit(self.worker_index, "无法获取 DeepSeek AI 响应流。")
                     self.signals.finished.emit(self.worker_index)
                     return

                chars_accumulated = ""
                last_emit_time = time.time()
                BUFFER_TIME = 0.1
                for chunk in stream:
                     if not self._is_running: break # 检查停止标志
                     if chunk.choices and chunk.choices[0].delta and chunk.choices[0].delta.content:
                        text_piece = chunk.choices[0].delta.content
                        current_time_ms = int(time.time() * 1000)
                        # logging.debug(f"DeepSeekRunnable {self.worker_index} received chunk at {current_time_ms}: '{text_piece[:20]}...'")
                        chars_accumulated += text_piece
                        current_time = time.time()
                        if chars_accumulated and (current_time - last_emit_time >= BUFFER_TIME):
                            # logging.debug(f"DeepSeekRunnable {self.worker_index} emitting signal at {int(time.time()*1000)}")
                            self.signals.text_received.emit(self.worker_index, chars_accumulated)
                            chars_accumulated = ""
                            last_emit_time = current_time
                if chars_accumulated:
                     # logging.debug(f"DeepSeekRunnable {self.worker_index} emitting final signal at {int(time.time()*1000)}")
                     self.signals.text_received.emit(self.worker_index, chars_accumulated)
            except Exception as e:
                logging.error(f"DeepSeekRunnable {self.worker_index} (ID: {id(self)}): 发生错误: {e}", exc_info=True)
                self.signals.error.emit(self.worker_index, f"DeepSeek错误: {e}")
            finally:
                logging.info(f"DeepSeekRunnable {self.worker_index} (ID: {id(self)}): Run method finished.")
                self.signals.finished.emit(self.worker_index) # 发送完成信号

# --- Google Runnable ---
if GOOGLE_AVAILABLE:
    class GoogleStreamRunnable(BaseRunnable):
        def __init__(self, worker_index: int, ai_service: GoogleAIService, prompt: str):
            super().__init__(worker_index)
            self.ai_service = ai_service
            self.prompt = prompt

        @Slot()
        def run(self):
            thread_id = threading.get_native_id()
            logging.info(f"GoogleRunnable {self.worker_index} (ID: {id(self)}) started in thread {thread_id}")
            if not self.ai_service or not self.ai_service.client:
                self.signals.error.emit(self.worker_index, "Google AI Service 未初始化。")
                self.signals.finished.emit(self.worker_index)
                return
            try:
                stream: Optional[Iterator[GenerateContentResponse]] = self.ai_service.get_completion_stream(self.prompt)
                if stream is None:
                     self.signals.error.emit(self.worker_index, "无法获取 Google AI 响应流。")
                     self.signals.finished.emit(self.worker_index)
                     return

                chars_accumulated = ""
                last_emit_time = time.time()
                BUFFER_TIME = 0.1
                for chunk in stream:
                     if not self._is_running: break
                     if hasattr(chunk, 'text') and chunk.text:
                        text_piece = chunk.text
                        current_time_ms = int(time.time() * 1000)
                        # logging.debug(f"GoogleRunnable {self.worker_index} received chunk at {current_time_ms}: '{text_piece[:20]}...'")
                        chars_accumulated += text_piece
                        current_time = time.time()
                        if chars_accumulated and (current_time - last_emit_time >= BUFFER_TIME):
                            # logging.debug(f"GoogleRunnable {self.worker_index} emitting signal at {int(time.time()*1000)}")
                            self.signals.text_received.emit(self.worker_index, chars_accumulated)
                            chars_accumulated = ""
                            last_emit_time = current_time
                if chars_accumulated:
                     # logging.debug(f"GoogleRunnable {self.worker_index} emitting final signal at {int(time.time()*1000)}")
                     self.signals.text_received.emit(self.worker_index, chars_accumulated)
            except Exception as e:
                 if isinstance(e, (GoogleAPICallError, RetryError)):
                     logging.error(f"GoogleRunnable {self.worker_index} (ID: {id(self)}): Google API 错误: {e}", exc_info=False)
                     self.signals.error.emit(self.worker_index, f"Google API错误: {e}")
                 else:
                     logging.error(f"GoogleRunnable {self.worker_index} (ID: {id(self)}): 发生错误: {e}", exc_info=True)
                     self.signals.error.emit(self.worker_index, f"Google错误: {e}")
            finally:
                logging.info(f"GoogleRunnable {self.worker_index} (ID: {id(self)}): Run method finished.")
                self.signals.finished.emit(self.worker_index)

# --- 主窗口 ---
class MainWindow(QMainWindow):
    def __init__(self, parent=None):
        super().__init__(parent)
        self.setWindowTitle("并发 AI 流式处理演示 (QThreadPool)")
        self.setGeometry(100, 100, 800, 700)

        # AI 服务实例
        self.deepseek_service = AIService() if OPENAI_AVAILABLE else None
        self.google_service = GoogleAIService() if GOOGLE_AVAILABLE else None
        self.current_service: Optional[Union[AIService, GoogleAIService]] = None

        # 使用 QThreadPool
        self.thread_pool = QThreadPool.globalInstance()
        logging.info(f"全局线程池最大线程数: {self.thread_pool.maxThreadCount()}")

        # 存储当前运行的 Runnable 实例，以便可以停止它们
        self.active_runnables: List[Optional[BaseRunnable]] = [None] * NUM_STREAMS
        self.active_tasks_count = 0 # 跟踪活动任务数量
        self.mutex = QMutex() # 用于保护 active_tasks_count

        self._init_ui()
        self._update_current_service()

    # _init_ui, _update_current_service 保持不变
    def _init_ui(self):
        central_widget = QWidget()
        self.setCentralWidget(central_widget)
        main_layout = QVBoxLayout(central_widget)
        service_layout = QHBoxLayout()
        service_layout.addWidget(QLabel("选择 AI 服务:"))
        self.service_combo = QComboBox()
        if self.deepseek_service and getattr(self.deepseek_service, 'client', None):
             self.service_combo.addItem("DeepSeek", userData=self.deepseek_service)
        if self.google_service and getattr(self.google_service, 'client', None):
             self.service_combo.addItem("Google Gemini", userData=self.google_service)
        service_layout.addWidget(self.service_combo)
        service_layout.addStretch()
        main_layout.addLayout(service_layout)
        self.service_combo.currentIndexChanged.connect(self._update_current_service)
        self.start_button = QPushButton(f"同时启动 {NUM_STREAMS} 个流")
        self.start_button.clicked.connect(self.start_all_streams)
        main_layout.addWidget(self.start_button)
        self.text_edits: List[QPlainTextEdit] = []
        for i in range(NUM_STREAMS):
            text_edit = QPlainTextEdit()
            text_edit.setReadOnly(True)
            text_edit.setPlaceholderText(f"流 {i+1} 的输出...")
            main_layout.addWidget(text_edit)
            self.text_edits.append(text_edit)

    @Slot()
    def _update_current_service(self):
        selected_data = self.service_combo.currentData()
        if selected_data:
            self.current_service = selected_data
            service_name = self.service_combo.currentText()
            logging.info(f"当前选择的 AI 服务: {service_name}")
            self.start_button.setEnabled(True)
            self.start_button.setText(f"使用 {service_name} 启动 {NUM_STREAMS} 个流")
        else:
            self.current_service = None
            logging.warning("没有可用的 AI 服务被选中或初始化失败。")
            self.start_button.setEnabled(False)
            self.start_button.setText("无可用 AI 服务")

    @Slot()
    def start_all_streams(self):
        if not self.current_service:
             QMessageBox.warning(self, "错误", "请先选择一个可用的 AI 服务。")
             return

        self._cleanup_runnables() # 清理旧的 runnable

        self.start_button.setEnabled(False)
        self.clear_all_displays()

        logging.info(f"准备使用 {self.service_combo.currentText()} 启动 {NUM_STREAMS} 个任务...")
        self.active_runnables = [None] * NUM_STREAMS # 重置列表
        self.active_tasks_count = NUM_STREAMS # 设置活动任务计数

        for i in range(NUM_STREAMS):
            prompt = DEMO_PROMPT_TEMPLATE.format(index=i+1)
            runnable: Optional[BaseRunnable] = None

            if isinstance(self.current_service, AIService) and OPENAI_AVAILABLE:
                runnable = DeepSeekStreamRunnable(i, self.current_service, prompt)
            elif isinstance(self.current_service, GoogleAIService) and GOOGLE_AVAILABLE:
                runnable = GoogleStreamRunnable(i, self.current_service, prompt)

            if runnable is None:
                logging.error(f"无法为索引 {i} 创建 Runnable")
                with QMutexLocker(self.mutex): # 保护计数器
                    self.active_tasks_count -= 1
                continue

            # 连接信号到主线程的槽
            runnable.signals.text_received.connect(self.handle_text_received)
            runnable.signals.error.connect(self.handle_error)
            runnable.signals.finished.connect(self.handle_finished) # 连接 finished 信号

            # 提交任务到线程池
            self.thread_pool.start(runnable)
            self.active_runnables[i] = runnable # 存储引用以便可以停止
            logging.info(f"已将任务 {i+1} 提交到线程池。")

    def clear_all_displays(self):
         for text_edit in self.text_edits:
             text_edit.clear()

    # handle_text_received, handle_error 基本不变
    @Slot(int, str)
    def handle_text_received(self, index: int, text: str):
        if 0 <= index < len(self.text_edits):
            target_edit = self.text_edits[index]
            if target_edit:
                try:
                    target_edit.insertPlainText(text)
                    target_edit.ensureCursorVisible()
                except RuntimeError as e:
                    logging.warning(f"更新索引 {index} 时出错: {e}")
        else:
            logging.error(f"收到无效索引 {index} 的文本。")

    @Slot(int, str)
    def handle_error(self, index: int, error_message: str):
        logging.error(f"任务 {index+1} 发生错误: {error_message}")
        if 0 <= index < len(self.text_edits):
             target_edit = self.text_edits[index]
             if target_edit:
                 try:
                     target_edit.append(f"<font color='red'>{error_message}</font>")
                 except RuntimeError as e:
                      logging.warning(f"追加错误 {index} 时出错: {e}")
        # 即使出错，也认为任务结束了
        self._task_finished(index)


    @Slot(int)
    def handle_finished(self, index: int):
        """处理 Runnable 完成的信号"""
        logging.info(f"任务 {index+1} 完成。")
        self._task_finished(index)

    def _task_finished(self, index: int):
        """内部方法，处理任务结束（正常或错误）"""
        with QMutexLocker(self.mutex):
             # 清理对应的 runnable 引用
             if 0 <= index < len(self.active_runnables):
                 self.active_runnables[index] = None
             self.active_tasks_count -= 1
             logging.debug(f"任务 {index+1} 结束，剩余活动任务: {self.active_tasks_count}")
             if self.active_tasks_count <= 0:
                 logging.info("所有任务处理完成。")
                 if self.start_button: # 确保按钮还存在
                     self.start_button.setEnabled(True)


    def _cleanup_runnables(self):
         """在开始新任务前，请求停止旧的 Runnable"""
         logging.debug("清理旧的 Runnable...")
         if hasattr(self, 'active_runnables') and self.active_runnables:
             for i in range(len(self.active_runnables)):
                 runnable = self.active_runnables[i]
                 if runnable:
                     logging.debug(f"请求停止旧 Runnable {i}")
                     runnable.request_stop()
                     self.active_runnables[i] = None # 清除引用
         # 不需要手动删除 Runnable，线程池会管理
         self.active_tasks_count = 0 # 重置计数器


    def closeEvent(self, event):
        logging.info("关闭窗口，等待线程池任务完成...")
        # 请求停止所有正在运行的任务
        self._cleanup_runnables()
        # 等待线程池完成所有任务（阻塞！）
        # 注意：在 GUI 关闭事件中长时间阻塞可能不是最佳实践，但对于 demo 可以接受
        # 更优雅的方式是禁用关闭，直到所有任务完成
        if self.thread_pool.activeThreadCount() > 0:
             logging.info(f"等待 {self.thread_pool.activeThreadCount()} 个活动线程完成...")
             # QThreadPool 没有简单的 waitForDone(timeout)
             # 我们可以简单等待一小段时间，或者实现更复杂的完成信号机制
             # 这里简单处理：允许关闭，后台任务可能未完成
             # self.thread_pool.waitForDone(3000) # 等待最多3秒
             pass
        logging.info("关闭事件处理完毕。")
        super().closeEvent(event)


# --- 程序入口 ---
if __name__ == "__main__":
    # 使 QThreadPool 能在退出时清理
    QCoreApplication.setAttribute(Qt.ApplicationAttribute.AA_EnableHighDpiScaling)
    QCoreApplication.setAttribute(Qt.ApplicationAttribute.AA_UseHighDpiPixmaps)

    app = QApplication(sys.argv)

    logging.basicConfig(
        level=logging.DEBUG, # DEBUG
        format='%(asctime)s - %(threadName)s [%(levelname)s] %(name)s: %(message)s', # 修改格式包含线程名/级别
        handlers=[logging.StreamHandler(sys.stdout)]
    )
    # 调整日志级别
    logging.getLogger("requests").setLevel(logging.WARNING)
    logging.getLogger("urllib3").setLevel(logging.WARNING)
    logging.getLogger("openai").setLevel(logging.INFO)
    logging.getLogger("google.api_core").setLevel(logging.INFO)
    logging.getLogger("google.auth").setLevel(logging.INFO)
    logging.getLogger("urllib3.connectionpool").setLevel(logging.INFO)
    logging.getLogger("httpcore").setLevel(logging.INFO) # 减少 httpcore 的 DEBUG 输出

    if not GOOGLE_AVAILABLE and not OPENAI_AVAILABLE:
         QMessageBox.critical(None, "错误", "google-generativeai 和 openai 库都未能成功导入。程序无法运行。")
         sys.exit(1)

    main_win = MainWindow()

    if not main_win.current_service:
         QMessageBox.critical(main_win, "初始化错误", "未能成功初始化任何 AI 服务。请检查 API Keys 和网络连接。")
         main_win.start_button.setEnabled(False)

    main_win.show()
    sys.exit(app.exec())