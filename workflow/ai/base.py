# workflows/ai/base.py
import logging
from abc import ABC, abstractmethod
from typing import List, Dict, AsyncIterator, Optional # <--- 返回类型改为 Optional[str]

class AbstractAIService(ABC):
    """AI 服务接口的抽象基类。"""

    # noinspection PyUnreachableCode
    @abstractmethod
    async def get_completion_stream(
        self,
        system_prompt: str,
        messages_history: List[Dict[str, str]]
    ) -> AsyncIterator[Optional[str]]: # <--- 修改返回类型提示
        """
        向 AI 发送请求并获取简化的异步流式文本响应。

        Args:
            system_prompt (str): 系统提示词。
            messages_history (List[Dict[str, str]]): 清理后的对话历史。

        Yields:
            str: AI 响应的文本块。
            None: 当流结束时，产生一个 None 值作为结束信号。
                 (注意: 实现也可以选择在流结束时不 yield None，
                  调用者通过迭代结束来判断流结束，但 yield None 更明确)

        Raises:
            # 可能抛出各种异常 (连接错误、认证错误、API 错误等)
            NotImplementedError: 如果子类未实现此方法。
        """
        # 为了让类型检查器和抽象方法正常工作，可以 yield 一个示例值
        # 但实际调用时应由子类覆盖
        if False: # 这段代码永远不会执行，只是为了满足类型提示和抽象要求
             yield "example text"
             yield None

    @abstractmethod
    async def get_completion_non_stream(
            self,
            system_prompt: str,
            messages_history: List[Dict[str, str]]
    ) -> Optional[str]:  # <--- 返回 Optional[str]
        """
        向 AI 发送请求并获取完整的非流式文本响应。

        Args:
            system_prompt (str): 系统提示词。
            messages_history (List[Dict[str, str]]): 清理后的对话历史。

        Returns:
            Optional[str]: 完整的 AI 响应文本，如果失败则返回 None 或抛出异常。

        Raises:
            # 可能抛出各种异常
            NotImplementedError: 如果子类未实现此方法。
        """
        raise NotImplementedError