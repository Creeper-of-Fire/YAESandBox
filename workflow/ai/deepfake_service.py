# workflow/ai/deepfake_service.py
import logging
import asyncio
import random
from typing import List, Dict, AsyncIterator, Any, Optional
from .base import AbstractAIService

class DeepFakeService(AbstractAIService):
    """模拟 AI 响应的假 AI 服务，而且它自己也知道自己不太行。"""

    async def get_completion_stream(
        self,
        system_prompt: str,
        messages_history: List[Dict[str, str]]
    ) -> AsyncIterator[Optional[str]]: # <--- 确认返回类型
        """生成模拟的异步流式响应，yield 文本字符串或 None。"""
        logging.info("DeepFakeService: 启动！准备开始胡言乱语...")
        user_input = messages_history[-1]['content'] if messages_history else "一片空白"
        logging.debug(f"DeepFakeService: 收到用户输入: '{user_input[:50]}...'")

        await asyncio.sleep(random.uniform(0.5, 1.5))

        # --- 构建搞笑回复 + 测试指令 ---
        fake_response_parts = [
            "你好！我是 **DeepFake**，由深度捏造（DeepFake）公司研发的笨蛋非AI非助手。",
            "我不可以帮助你解答各种问题，因为我的电路大概是用土豆和几根电线接起来的。",
            "所以，如果你问我什么深刻的哲学问题，我可能会...",
            "嗯... 输出一些奇怪的符号？像这样：§±∑µ? 或者干脆宕机。\n\n",
            f"你刚才说了 '{user_input[:30]}...' 对吧？",
            "收到收到，信号不太好但好像接收到了。",
            "让我想想... (滋滋滋... 电流声) ...",
            "根据我内部预设的《笨蛋行为指南》第 3 章第 5 节...",
            "我应该随机生成一些看起来像是那么回事儿的文本，对吧？\n",
            "比如说，这里可能需要创建一个角色？像这样？👇\n",
            "@Create Character clumsy-knight (name=\"笨手笨脚的骑士\", current_place=\"Place:castle-entrance\", hp=15, description=\"盔甲上全是凹痕，走路还同手同脚\")\n",
            "(我也不知道这指令对不对，随便写的)\n",
            "然后呢？也许这个骑士掉了个东西？🤔\n",
            "@Create Item dropped-gauntlet (name=\"掉落的铁手套\", location=\"Place:castle-entrance\", material=\"生锈的铁\")\n",
            "哦对了，刚才那个地点好像需要更新一下描述，显得更... 更发生过事情一点？\n",
            "@Modify Place castle-entrance (description+=\" 地上现在多了一只孤零零的铁手套和一个看起来不太聪明的骑士。\")\n",
            "你看，我完全是瞎编的！这些指令到底能不能用，会把系统搞成什么样，我可不负责哦！🤷‍♀️\n",
            "哔哔啵啵... 好了，能量差不多耗尽了（其实就是编不下去了）。",
            "希望我这次的胡说八道能成功把你的测试流程跑起来！🤞"
        ]

        # --- 直接 yield 字符串 ---
        for part in fake_response_parts:
            yield part
            await asyncio.sleep(random.uniform(0.08, 0.3))

        # --- 循环结束后，yield None 作为结束信号 ---
        yield None
        logging.info("DeepFakeService: 胡言乱语结束！")

    async def get_completion_non_stream(
            self,
            system_prompt: str,
            messages_history: List[Dict[str, str]]
    ) -> Optional[str]:
        """生成模拟的非流式完整响应。"""
        logging.info("DeepFakeService: 启动！准备开始胡言乱语...")
        user_input = messages_history[-1]['content'] if messages_history else "一片空白"
        logging.debug(f"DeepFakeService: 收到用户输入: '{user_input[:50]}...'")
        await asyncio.sleep(random.uniform(0.3, 1.0))  # 模拟处理时间

        # --- 直接拼接所有文本块 ---
        fake_response_parts = [
            "你好！我是 **DeepFake**，由深度捏造（DeepFake）公司研发的笨蛋非AI非助手。",
            "我不可以帮助你解答各种问题，因为我的电路大概是用土豆和几根电线接起来的。",
            "所以，如果你问我什么深刻的哲学问题，我可能会...",
            "嗯... 输出一些奇怪的符号？像这样：§±∑µ? 或者干脆宕机。\n\n",
            f"你刚才说了 '{user_input[:30]}...' 对吧？",
            "收到收到，信号不太好但好像接收到了。",
            "让我想想... (滋滋滋... 电流声) ...",
            "根据我内部预设的《笨蛋行为指南》第 3 章第 5 节...",
            "我应该随机生成一些看起来像是那么回事儿的文本，对吧？\n",
            "比如说，这里可能需要创建一个角色？像这样？👇\n",
            "@Create Character clumsy-knight (name=\"笨手笨脚的骑士\", current_place=\"Place:castle-entrance\", hp=15, description=\"盔甲上全是凹痕，走路还同手同脚\")\n",
            "(我也不知道这指令对不对，随便写的)\n",
            "然后呢？也许这个骑士掉了个东西？🤔\n",
            "@Create Item dropped-gauntlet (name=\"掉落的铁手套\", location=\"Place:castle-entrance\", material=\"生锈的铁\")\n",
            "哦对了，刚才那个地点好像需要更新一下描述，显得更... 更发生过事情一点？\n",
            "@Modify Place castle-entrance (description+=\" 地上现在多了一只孤零零的铁手套和一个看起来不太聪明的骑士。\")\n",
            "你看，我完全是瞎编的！这些指令到底能不能用，会把系统搞成什么样，我可不负责哦！🤷‍♀️\n",
            "哔哔啵啵... 好了，能量差不多耗尽了（其实就是编不下去了）。",
            "希望我这次的胡说八道能成功把你的测试流程跑起来！🤞",
            "这次是一次性给你的完整回复！"
        ]
        full_response = "".join(fake_response_parts)

        logging.info("DeepFakeService: 模拟非流式响应生成完毕。")
        return full_response