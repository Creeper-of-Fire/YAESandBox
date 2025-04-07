# simple_deepseek_concurrency_test_raw_http_multi_key.py
import sys
import logging
import time
import threading
import json
from typing import List, Optional, Dict, Iterator

try:
    import httpx
except ImportError:
    logging.error("请先安装 httpx 库: pip install httpx")
    sys.exit(1)

# --- 配置 ---
LOG_LEVEL = logging.DEBUG
NUM_THREADS = 2 # 必须是 2，因为我们只有两个 Key
DEMO_PROMPT_TEMPLATE = "写一首关于数字 {index} 的800字长诗。"
DEEPSEEK_API_URL = "https://api.deepseek.com/chat/completions"
DEEPSEEK_MODEL = "deepseek-chat"

# --- 不同的 API Keys ---
API_KEYS = [
    "sk-ee30faa083f94e71837a36cf5f870eab", # 你的第一个 Key
    "sk-8c772352e3fe4a22bfcce427c87a2ede", # 你的第二个 Key
]

if len(API_KEYS) < NUM_THREADS:
    logging.error(f"API Keys 数量 ({len(API_KEYS)}) 少于请求的线程数 ({NUM_THREADS})。")
    sys.exit(1)

# --- 原始 API 调用函数 (保持不变) ---
def call_deepseek_api_raw(api_key: str, model: str, messages: List[Dict[str, str]]) -> Iterator[str]:
    """直接使用 httpx 调用 DeepSeek API 并处理 SSE 流。"""
    headers = {
        "Authorization": f"Bearer {api_key}",
        "Content-Type": "application/json",
        "Accept": "text/event-stream",
    }
    payload = {"model": model, "messages": messages, "stream": True}
    timeout = httpx.Timeout(5.0, read=120.0)
    try:
        with httpx.stream("POST", DEEPSEEK_API_URL, headers=headers, json=payload, timeout=timeout) as response:
            if response.status_code != 200:
                error_body = "".join([chunk for chunk in response.iter_text(max_chars=512)])
                raise httpx.HTTPStatusError(f"API 请求失败，状态码: {response.status_code}, 响应体: {error_body}", request=response.request, response=response)
            logging.debug(f"线程 {threading.current_thread().name}: 开始读取 SSE 流 (Key: ...{api_key[-4:]})") # 日志中显示 Key 的最后几位
            for line in response.iter_lines():
                if line.startswith("data:"):
                    data_content = line[len("data:"):].strip()
                    if data_content == "[DONE]":
                        logging.debug(f"线程 {threading.current_thread().name}: 收到 [DONE] 信号。")
                        break
                    if data_content:
                        try:
                            chunk_data = json.loads(data_content)
                            if (isinstance(chunk_data, dict) and "choices" in chunk_data and
                                isinstance(chunk_data["choices"], list) and len(chunk_data["choices"]) > 0 and
                                isinstance(chunk_data["choices"][0], dict) and "delta" in chunk_data["choices"][0] and
                                isinstance(chunk_data["choices"][0]["delta"], dict) and "content" in chunk_data["choices"][0]["delta"]):
                                text_piece = chunk_data["choices"][0]["delta"]["content"]
                                if text_piece: yield text_piece
                        except json.JSONDecodeError: logging.warning(f"线程 {threading.current_thread().name}: 无法解析 JSON: {data_content}")
                        except Exception as parse_err: logging.warning(f"线程 {threading.current_thread().name}: 解析块时出错: {parse_err} (Data: {data_content})")
                elif line.startswith(":"): pass
                elif line.strip(): logging.warning(f"线程 {threading.current_thread().name}: 收到非预期行: {line}")
            logging.debug(f"线程 {threading.current_thread().name}: SSE 流读取完毕。")
    except httpx.TimeoutException as e: logging.error(f"线程 {threading.current_thread().name}: 请求超时: {e}"); raise
    except httpx.RequestError as e: logging.error(f"线程 {threading.current_thread().name}: 请求错误: {e}"); raise
    except Exception as e: logging.error(f"线程 {threading.current_thread().name}: 处理时未知错误: {e}", exc_info=True); raise

# --- 工作函数 (接受 api_key) ---
def worker_task(index: int, api_key: str):
    thread_name = threading.current_thread().name
    native_thread_id = threading.get_native_id()
    logging.info(f"{thread_name} (Index: {index}, OS ID: {native_thread_id}, Key: ...{api_key[-4:]}): 开始任务...") # 日志显示 Key
    start_time = time.time()
    try:
        system_prompt_content = "你是一个乐于助人的助手。"
        user_prompt_content = DEMO_PROMPT_TEMPLATE.format(index=index+1)
        messages = [{"role": "system", "content": system_prompt_content}, {"role": "user", "content": user_prompt_content}]
        logging.info(f"{thread_name}: 调用原始 DeepSeek API 函数 (Key: ...{api_key[-4:]})...")
        chunk_count = 0
        for text_piece in call_deepseek_api_raw(api_key, DEEPSEEK_MODEL, messages):
            chunk_count += 1
            current_time_ms = int(time.time() * 1000)
            logging.debug(f"{thread_name} received text chunk #{chunk_count} at {current_time_ms}: '{text_piece[:30].replace(chr(10), '')}...'")
        logging.info(f"{thread_name}: 文本流处理完成，共 {chunk_count} 个有效块。")
    except Exception as e: logging.error(f"{thread_name}: 任务执行失败: {e}")
    finally:
        end_time = time.time()
        duration = end_time - start_time
        logging.info(f"{thread_name} (Index: {index}, Key: ...{api_key[-4:]}): 任务完成，耗时: {duration:.2f} 秒。")

# --- 主程序 ---
if __name__ == "__main__":
    logging.basicConfig(
        level=LOG_LEVEL, # DEBUG
        format='%(asctime)s - %(threadName)s [%(levelname)s] %(name)s: %(message)s',
        handlers=[logging.StreamHandler(sys.stdout)]
    )
    logging.getLogger("httpx").setLevel(logging.INFO)
    logging.getLogger("httpcore").setLevel(logging.INFO)

    # 创建线程列表
    threads: List[threading.Thread] = []
    logging.info(f"准备创建并启动 {NUM_THREADS} 个工作线程，使用不同的 API Keys...")
    for i in range(NUM_THREADS):
        api_key_to_use = API_KEYS[i] # 每个线程使用不同的 Key
        t = threading.Thread(target=worker_task, args=(i, api_key_to_use), name=f"WorkerThread-{i}")
        threads.append(t)

    # 启动所有线程
    for t in threads:
        t.start()

    # 等待所有线程完成
    logging.info("等待所有工作线程完成...")
    for t in threads:
        t.join()

    logging.info("所有工作线程已完成。")