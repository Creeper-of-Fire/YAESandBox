# simple_deepseek_concurrency_test.py
import sys
import logging
import time
import threading
from typing import List, Optional, Dict

# --- 复用 core.ai_service 中的 DeepSeek 部分 ---
# 假设 ai_service.py 在 core 包中
try:
    from core.ai_service import AIService as DeepSeekAIService
    from openai.types.chat.chat_completion_chunk import ChatCompletionChunk
    from openai import APITimeoutError, APIConnectionError, RateLimitError, APIStatusError
    OPENAI_AVAILABLE = True
except ImportError:
    logging.error("无法导入 DeepSeekAIService 或相关类型。请确保 core 包在 Python 路径中且 ai_service.py 存在，并且 openai 库已安装。")
    sys.exit(1)

# --- 配置 ---
LOG_LEVEL = logging.DEBUG
NUM_THREADS = 2 # 测试的并发线程数
DEMO_PROMPT_TEMPLATE = "写一首关于数字 {index} 的100字长诗。" # 长提示

# --- 工作函数 ---
def worker_task(index: int, ai_service: DeepSeekAIService):
    """
    在单独线程中执行 DeepSeek 流式请求并打印接收日志。
    """
    thread_name = threading.current_thread().name # 获取线程名称
    native_thread_id = threading.get_native_id() # 获取 OS 线程 ID
    logging.info(f"{thread_name} (Index: {index}, OS ID: {native_thread_id}): 开始任务...")
    start_time = time.time()

    if not ai_service or not ai_service.client:
        logging.error(f"{thread_name}: AI Service 未初始化。任务终止。")
        return

    try:
        system_prompt = "你是一个乐于助人的助手。"
        prompt = DEMO_PROMPT_TEMPLATE.format(index=index+1)
        messages_history: List[Dict[str, str]] = [{"role": "user", "content": prompt}]

        logging.info(f"{thread_name}: 请求 DeepSeek API...")
        stream = ai_service.get_completion_stream(system_prompt, messages_history)

        if stream is None:
            logging.error(f"{thread_name}: 无法获取 AI 响应流。")
        else:
            logging.info(f"{thread_name}: 开始接收流数据...")
            chunk_count = 0
            for chunk in stream:
                chunk_count += 1
                if chunk.choices and chunk.choices[0].delta and chunk.choices[0].delta.content:
                    text_piece = chunk.choices[0].delta.content
                    current_time_ms = int(time.time() * 1000)
                    logging.debug(f"{thread_name} received chunk #{chunk_count} at {current_time_ms}: '{text_piece[:30].replace(chr(10), '')}...'") # 打印时间戳和块内容
                # 可选：加入微小延迟观察交错，但为了纯粹测试，先不加
                # time.sleep(0.01)
            logging.info(f"{thread_name}: 流接收完成，共 {chunk_count} 个块。")

    except (APITimeoutError, APIConnectionError, RateLimitError, APIStatusError) as e:
         logging.error(f"{thread_name}: 调用 DeepSeek AI API 时出错: {e}")
    except Exception as e:
        logging.error(f"{thread_name}: 发生未预料的错误: {e}", exc_info=True)
    finally:
        end_time = time.time()
        duration = end_time - start_time
        logging.info(f"{thread_name} (Index: {index}): 任务完成，耗时: {duration:.2f} 秒。")


# --- 主程序 ---
if __name__ == "__main__":
    logging.basicConfig(
        level=LOG_LEVEL, # DEBUG
        format='%(asctime)s - %(threadName)s [%(levelname)s] %(name)s: %(message)s',
        handlers=[logging.StreamHandler(sys.stdout)]
    )
    # 设置 OpenAI/DeepSeek 相关库的日志级别，避免过多干扰
    logging.getLogger("openai").setLevel(logging.INFO)
    logging.getLogger("httpcore").setLevel(logging.INFO)
    logging.getLogger("httpx").setLevel(logging.INFO)
    logging.getLogger("urllib3").setLevel(logging.INFO)


    if not OPENAI_AVAILABLE:
        logging.critical("OpenAI 库不可用，无法运行测试。")
        sys.exit(1)

    # 1. 初始化共享的 AIService
    logging.info("初始化 DeepSeekAIService...")
    shared_ai_service = DeepSeekAIService()
    if not shared_ai_service or not shared_ai_service.client:
        logging.critical("无法初始化 DeepSeekAIService，请检查 API Key 或网络。测试终止。")
        sys.exit(1)

    # 2. 创建线程列表
    threads: List[threading.Thread] = []
    logging.info(f"准备创建并启动 {NUM_THREADS} 个工作线程...")
    for i in range(NUM_THREADS):
        # 使用 lambda 传递参数给 target 函数
        # 注意：lambda 捕获的是 i 的最终值，如果直接用 i 会有问题，但这里 worker_task 内部使用 index
        # 更安全的方式是：
        # t = threading.Thread(target=worker_task, args=(i, shared_ai_service), name=f"WorkerThread-{i}")
        # 为清晰起见，使用 args
        t = threading.Thread(target=worker_task, args=(i, shared_ai_service), name=f"WorkerThread-{i}")
        threads.append(t)

    # 3. 启动所有线程
    for t in threads:
        t.start()

    # 4. 等待所有线程完成
    logging.info("等待所有工作线程完成...")
    for t in threads:
        t.join() # 阻塞主线程直到该线程结束

    logging.info("所有工作线程已完成。")