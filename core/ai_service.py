# core/ai_service.py
import os
import logging
from typing import List, Dict, Iterator, Optional, Any # Iterator 用于流式响应
from openai import OpenAI, APITimeoutError, APIConnectionError, RateLimitError, APIStatusError

# --- 配置 ---
# 建议从环境变量或配置文件读取 API Key
# DEEPSEEK_API_KEY = os.getenv("DEEPSEEK_API_KEY")
# 在原型阶段，可以直接在此处填入你的 Key，在原型阶段以后这个key会被用户注销，所以没关系
DEEPSEEK_API_KEY = "sk-ee30faa083f94e71837a36cf5f870eab" # <--- 在这里填入你的 DeepSeek API Key
if not DEEPSEEK_API_KEY or "sk-xxx" in DEEPSEEK_API_KEY:
    logging.warning("请在 ai_service.py 文件中或环境变量中设置有效的 DEEPSEEK_API_KEY")

# DeepSeek API 的基础 URL
DEEPSEEK_BASE_URL = "https://api.deepseek.com" # 确保使用正确的 URL

# 要使用的模型名称
MODEL_NAME = "deepseek-chat" # 确认模型名称

class AIService:
    """封装与 AI 模型 (DeepSeek) 的通信服务。"""
    def __init__(self, api_key: Optional[str] = None, base_url: str = DEEPSEEK_BASE_URL, model: str = MODEL_NAME):
        """
        初始化 AI 服务客户端。

        Args:
            api_key (Optional[str]): DeepSeek API Key. 如果为 None，则尝试从全局配置获取。
            base_url (str): DeepSeek API 的基础 URL.
            model (str): 要使用的模型名称.
        """
        resolved_api_key = api_key if api_key else DEEPSEEK_API_KEY

        if not resolved_api_key or "sk-xxx" in resolved_api_key:
            logging.error("AI 服务初始化失败：未提供有效的 API Key。")
            self.client: Optional[OpenAI] = None # 明确类型
        else:
            try:
                # 传递 api_key 和 base_url 给 OpenAI 客户端
                self.client = OpenAI(api_key=resolved_api_key, base_url=base_url)
                logging.info(f"OpenAI 客户端初始化成功。模型: {model}, Base URL: {base_url}")
            except Exception as e:
                logging.error(f"初始化 OpenAI 客户端时出错: {e}")
                self.client = None

        self.model_name = model

    def get_completion_stream(self, system_prompt: str, messages_history: List[Dict[str, str]]) -> Optional[Iterator[Any]]:
        """
        向 AI 发送请求并获取流式响应。

        Args:
            system_prompt (str): 系统提示词。
            messages_history (List[Dict[str, str]]): 清理后的对话历史。

        Returns:
            Optional[Iterator[Any]]: AI 响应的流迭代器，或在失败时返回 None。
        """
        if not self.client:
            logging.error("无法获取 AI 响应：AI 客户端未初始化。")
            return None

        request_messages = [{"role": "system", "content": system_prompt}] + messages_history
        logging.info(f"准备向 AI 发送 {len(request_messages)} 条消息。模型: {self.model_name}")
        # logging.debug(f"完整请求消息体: {request_messages}") # Debug

        try:
            response_stream = self.client.chat.completions.create(
                model=self.model_name,
                messages=request_messages,
                stream=True,
                # temperature=0.7, # 可选参数
                # max_tokens=2048, # 可选参数
            )
            logging.info("已成功发送请求到 AI，开始接收流式响应...")
            return response_stream

        except APITimeoutError as e:
            logging.error(f"请求 AI API 超时: {e}")
            raise # 原型阶段让其崩溃
        except APIConnectionError as e:
            logging.error(f"无法连接到 AI API: {e}")
            raise
        except RateLimitError as e:
            logging.error(f"达到 AI API 速率限制: {e}")
            raise
        except APIStatusError as e:
            logging.error(f"AI API 返回错误状态: Status={e.status_code}, Response={e.response}")
            raise
        except Exception as e:
            logging.error(f"调用 AI API 时发生未预料的错误: {e}", exc_info=True)
            raise