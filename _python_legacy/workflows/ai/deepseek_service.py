# workflows/ai/deepseek_service.py
import logging
import os
from typing import List, Dict, AsyncIterator, Optional

from openai import AsyncOpenAI, AuthenticationError, PermissionDeniedError

from workflows.ai.base import AbstractAIService

# 使用 AsyncOpenAI

# 从环境变量加载配置 (将在应用启动时完成，这里先定义好)
# DEEPSEEK_API_KEY = os.getenv("DEEPSEEK_API_KEY") # 不在这里直接加载
DEEPSEEK_BASE_URL = "https://api.deepseek.com"
MODEL_NAME = "deepseek-chat"


class DeepSeekService(AbstractAIService):
    """使用 DeepSeek API 的真实 AI 服务实现。"""

    def __init__(self, api_key: Optional[str] = None, base_url: str = DEEPSEEK_BASE_URL, model: str = MODEL_NAME):
        resolved_api_key = api_key or os.getenv("DEEPSEEK_API_KEY")

        # 在 __init__ 中就检查 Key，如果无效，可以提前失败或记录警告
        if not resolved_api_key or "sk-xxx" in resolved_api_key:
            logging.error("DeepSeek 服务初始化失败：未提供有效的 API Key。请检查 .env 文件或环境变量。服务将不可用。")
            self.client: Optional[AsyncOpenAI] = None
            self.initialized_properly = False  # 添加一个标志
        else:
            try:
                self.client = AsyncOpenAI(api_key=resolved_api_key, base_url=base_url)
                logging.info(f"AsyncOpenAI 客户端初始化成功 (DeepSeek)。模型: {model}, Base URL: {base_url}")
                self.initialized_properly = True
            except Exception as e:
                logging.error(f"初始化 AsyncOpenAI 客户端 (DeepSeek) 时出错: {e}")
                self.client = None
                self.initialized_properly = False

        self.model_name = model

    async def get_completion_stream(
            self,
            system_prompt: str,
            messages_history: List[Dict[str, str]]
    ) -> AsyncIterator[Optional[str]]:  # <--- 确认返回类型
        """向 DeepSeek 发送请求，处理 chunk，yield 文本字符串或 None。"""
        if not self.initialized_properly or not self.client:
            logging.error("DeepSeek 服务未正确初始化或客户端不可用。")
            raise RuntimeError("DeepSeekService is not properly initialized or client is unavailable.")

        request_messages = [{"role": "system", "content": system_prompt}] + messages_history
        logging.info(f"准备向 DeepSeek 发送 {len(request_messages)} 条消息。模型: {self.model_name}")

        finish_reason = None  # 用于记录结束原因
        try:
            response_stream = await self.client.chat.completions.create(
                model=self.model_name,
                messages=request_messages,
                stream=True,
            )
            logging.info("已成功发送请求到 DeepSeek，开始处理流式响应...")

            # 迭代原始的 chunk 对象
            async for chunk in response_stream:
                content = None
                chunk_finish_reason = None
                try:
                    # --- 在这里处理 ChatCompletionChunk 对象 ---
                    if chunk and chunk.choices:
                        choice = chunk.choices[0]
                        if choice.delta:
                            content = choice.delta.content
                        chunk_finish_reason = choice.finish_reason  # 获取当前 chunk 的结束原因
                except (AttributeError, IndexError, TypeError) as e:
                    logging.warning(f"处理 DeepSeek chunk 对象时出错或结构不符: {e} - Chunk: {chunk}")
                    continue  # 跳过这个有问题的 chunk

                # --- 只 yield 有效的文本内容 ---
                # 注意：即使 content 是空字符串 ""，也要 yield，因为它是有效的增量
                if content is not None:
                    yield content

                # --- 记录并检查结束原因 ---
                if chunk_finish_reason:
                    finish_reason = chunk_finish_reason
                    logging.info(f"DeepSeek 流结束，原因: {finish_reason}")
                    break  # 流已结束，跳出循环

            # --- 循环结束后 (正常结束或 break)，yield None 作为结束信号 ---
            yield None
            logging.info("DeepSeekService get_completion_stream 处理完毕。")


        # --- 异常处理保持不变，因为它们是通信层面的问题 ---
        except AuthenticationError as e:
            logging.error(f"DeepSeek API 认证失败: {e}")
            # 在 yield None 之前就出错了，所以迭代会在这里中断
            raise  # 重新抛出，让上层处理
        except PermissionDeniedError as e:
            logging.error(f"DeepSeek API 权限错误: {e}")
            raise
        # ... (其他异常处理) ...
        except Exception as e:
            logging.error(f"调用 DeepSeek API 或处理流时发生未预料的错误: {e}", exc_info=True)
            raise

    async def get_completion_non_stream(
            self,
            system_prompt: str,
            messages_history: List[Dict[str, str]]
    ) -> Optional[str]:
        """获取 DeepSeek 的非流式响应。"""
        if not self.initialized_properly or not self.client:
            logging.error("DeepSeek 服务未正确初始化或客户端不可用。")
            raise RuntimeError("DeepSeekService is not properly initialized or client is unavailable.")

        request_messages = [{"role": "system", "content": system_prompt}] + messages_history
        logging.info(f"向 DeepSeek 发送非流式请求...")

        try:
            response = await self.client.chat.completions.create(
                model=self.model_name,
                messages=request_messages,
                stream=False  # <--- 设置为 False
            )
            logging.info("成功收到 DeepSeek 非流式响应。")

            # 提取内容
            if response.choices and response.choices[0].message:
                return response.choices[0].message.content
            else:
                logging.warning("DeepSeek 非流式响应结构不符合预期或无内容。")
                return None

        # --- 异常处理保持不变，因为它们是通信层面的问题 ---
        except AuthenticationError as e:
            logging.error(f"DeepSeek API 认证失败: {e}")
            # 在 yield None 之前就出错了，所以迭代会在这里中断
            raise  # 重新抛出，让上层处理
        except PermissionDeniedError as e:
            logging.error(f"DeepSeek API 权限错误: {e}")
            raise
        # ... (其他异常处理) ...
        except Exception as e:
            logging.error(f"调用 DeepSeek API 或处理流时发生未预料的错误: {e}", exc_info=True)
            raise
