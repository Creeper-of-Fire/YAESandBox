# ai_service.py
import os
import logging
from typing import List, Dict, Iterator, Optional, Any # Iterator 用于流式响应
from openai import OpenAI, APITimeoutError, APIConnectionError, RateLimitError, APIStatusError

# --- 配置 ---
# 建议从环境变量或配置文件读取 API Key
# DEEPSEEK_API_KEY = os.getenv("DEEPSEEK_API_KEY")
# 在原型阶段，可以直接在此处填入你的 Key，但请注意安全风险
DEEPSEEK_API_KEY = "sk-ee30faa083f94e71837a36cf5f870eab" # <--- 在这里填入你的 DeepSeek API Key
if not DEEPSEEK_API_KEY or "sk-xxx" in DEEPSEEK_API_KEY:
    logging.warning("请在 ai_service.py 文件中设置有效的 DEEPSEEK_API_KEY")

# DeepSeek API 的基础 URL
# 注意：官方文档可能会更新 URL，请确认使用最新或最稳定的版本
# 例如可能是 https://api.deepseek.com/v1
DEEPSEEK_BASE_URL = "https://api.deepseek.com" # 请根据需要调整为包含 /v1 的版本

# 要使用的模型名称
MODEL_NAME = "deepseek-chat"

class AIService:
    """
    封装与 AI 模型 (DeepSeek) 的通信服务。
    只负责发送请求和返回流式响应，不处理内容。
    """

    def __init__(self, api_key: str = DEEPSEEK_API_KEY, base_url: str = DEEPSEEK_BASE_URL, model: str = MODEL_NAME):
        """
        初始化 AI 服务客户端。

        Args:
            api_key (str): DeepSeek API Key.
            base_url (str): DeepSeek API 的基础 URL.
            model (str): 要使用的模型名称.
        """
        if not api_key or "sk-xxx" in api_key:
            # 在原型阶段，允许在没有 key 的情况下继续，但会记录错误并在调用时失败
            logging.error("AI 服务初始化失败：未提供有效的 API Key。")
            self.client = None
        else:
            try:
                self.client = OpenAI(api_key=api_key, base_url=base_url)
                logging.info(f"OpenAI 客户端初始化成功。Base URL: {base_url}")
            except Exception as e:
                logging.error(f"初始化 OpenAI 客户端时出错: {e}")
                self.client = None # 确保客户端状态明确

        self.model_name = model

    def get_completion_stream(self, system_prompt: str, messages_history: List[Dict[str, str]]) -> Optional[Iterator[Any]]:
        """
        向 AI 发送请求并获取流式响应。

        Args:
            system_prompt (str): 系统提示词，定义 AI 的角色和行为。
            messages_history (List[Dict[str, str]]): 对话历史记录，
                格式为 [{"role": "user", "content": "..."}, {"role": "assistant", "content": "..."}]。

        Returns:
            Optional[Iterator[Any]]: 一个迭代器对象，可以逐块获取 AI 的响应。
                                     如果客户端未初始化或 API 调用失败，则返回 None。
                                     迭代器产生的是 OpenAI 库定义的 chunk 对象。
        """
        if not self.client:
            logging.error("无法获取 AI 响应：AI 客户端未初始化。")
            return None

        # 组合系统提示词和对话历史
        request_messages = [{"role": "system", "content": system_prompt}] + messages_history
        logging.info(f"准备向 AI 发送 {len(request_messages)} 条消息。最后一条: {request_messages[-1]['role']}")
        # logging.debug(f"完整请求消息体: {request_messages}") # 调试时可以取消注释

        try:
            # 调用 API，启用流式传输
            response_stream = self.client.chat.completions.create(
                model=self.model_name,
                messages=request_messages,
                stream=True,
                # 可以根据需要添加其他参数，例如：
                # temperature=0.7,  # 控制创造性，0 表示确定性，更高值更随机
                # max_tokens=2048, # 限制 AI 回复的最大长度
            )
            logging.info("已成功发送请求到 AI，开始接收流式响应...")
            return response_stream # 直接返回流对象

        # --- 异常处理（原型阶段简化，直接让程序崩溃抛出堆栈） ---
        # 以下是更健壮的处理方式，但在原型阶段我们让它们直接 raise
        except APITimeoutError as e:
            logging.error(f"请求 AI API 超时: {e}")
            raise # 崩溃
        except APIConnectionError as e:
            logging.error(f"无法连接到 AI API: {e}")
            raise # 崩溃
        except RateLimitError as e:
            logging.error(f"达到 AI API 速率限制: {e}")
            raise # 崩溃
        except APIStatusError as e:
            logging.error(f"AI API 返回错误状态: Status={e.status_code}, Response={e.response}")
            raise # 崩溃
        except Exception as e:
            # 捕获其他可能的配置或库错误
            logging.error(f"调用 AI API 时发生未预料的错误: {e}")
            raise # 崩溃

# --- 使用示例 (仅用于演示，实际调用在主程序中) ---
if __name__ == '__main__':
    # 配置日志记录
    logging.basicConfig(level=logging.INFO, format='%(asctime)s - %(levelname)s - %(message)s')

    # 创建服务实例
    ai_service = AIService()

    # 检查服务是否成功初始化
    if ai_service.client:
        # 准备测试数据
        test_system_prompt = "你是一个乐于助人的助手。"
        test_history = [{"role": "user", "content": "你好，世界！"}]

        # 获取流式响应
        stream = ai_service.get_completion_stream(test_system_prompt, test_history)

        # 处理流式响应
        if stream:
            print("AI 响应 (流式):")
            full_response_content = ""
            try:
                for chunk in stream:
                    # 从 chunk 中提取内容
                    # 注意: chunk 的结构取决于所使用的 OpenAI 库版本和 API 响应
                    # 通常内容在 chunk.choices[0].delta.content
                    delta_content = chunk.choices[0].delta.content
                    if delta_content:
                        print(delta_content, end="", flush=True) # 实时打印
                        full_response_content += delta_content

                print("\n--- 流接收完毕 ---")
                print(f"完整响应内容:\n{full_response_content}")
            except Exception as e:
                logging.error(f"处理 AI 响应流时出错: {e}")
                # 在原型阶段，这里也可能希望直接 raise
        else:
            print("未能获取 AI 响应流。")
    else:
        print("AI 服务未能初始化，无法执行示例。请检查 API Key 和网络连接。")