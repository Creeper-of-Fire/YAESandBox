# prompts.py
import logging
import re
from typing import Dict, List, Any, Optional, Set

import yaml

# 导入 GameState 和 WorldState 用于类型提示和访问数据
from game_state import GameState
from world_state import WorldState, Place, Character, Item  # 需要实体类用于类型检查

# --- 核心系统提示词模板 ---
# 使用 f-string 模板，占位符将在 get_system_prompt 中填充
SYSTEM_PROMPT_TEMPLATE = """\
你被放置在一个基于文本的 RPG 游戏引擎中，你是游戏世界的创造者和叙述者。你的核心任务是响应用户的输入，描绘生动的场景，并动态地塑造这个世界。

{problematic_entities_report_placeholder}

**交互流程:**
*   系统会提供清理过的对话历史（移除指令）和基于用户当前焦点的世界状态摘要 (YAML 格式)。
*   **当前用户的焦点是: {user_focus_description}**
*   **请将你的叙述集中在用户焦点相关的实体和地点上。**
*   你的回复应包含叙述文本和必要的 `@Command` 指令来更新世界状态。

**当前世界状态摘要 (仅供参考):**
{world_summary_placeholder}

# 世界交互规范手册
{interaction_manual_placeholder} # 将手册也提取出来，便于管理
"""
# --- 交互规范手册内容 ---
INTERACTION_MANUAL = """
## 一、基础原则
1.  **指令驱动原则**：所有游戏状态变更必须通过显式指令实现
2.  **即时同步原则**：每次指令执行后需确保世界状态完全同步
3.  **显式创建原则**：所有实体必须经过明确创建指令才能存在
4.  **没有主角原则**：不区分NPC和玩家，因为用户随时可以切换自己的身份

## 二、指令规范详解
（所有指令参数都应包裹在**圆括号 `()`** 中，不再支持方括号 `[]`）

### 1. 创建指令（@Create）

#### 1.0 属性解释
- `<entity_id>`: 必须是唯一的英文小写字母、数字和连字符（kebab-case），例如 `village-inn`, `rusty-sword`, `goblin-chief`。尽量不要使用 `player`。
- `name`: 实体的称呼，**必填参数**。如果暂时未知，可用描述性名称，如 `"未知药水"`、`"散发可怕气息的书籍"`，之后用 `@Modify` 修改。
- `location` (物品的位置，可以是地点ID或角色ID，格式：`"Place:地点ID"` 或 `"Character:角色ID"`)
- `current_place` (角色的位置，必须是地点 ID，格式：`"Place:地点ID"`)
- 自定义属性：如 `description="描述文本"`,`owner="盗贼工会"`,`心情=10`,`好感度=-100`, `hp=10`, `is_locked=true`。内容完全由你定义，用于丰富实体。**属性值可以是字符串（加引号）、整数、布尔值（true/false）、列表 `[1, "a", true]`、字典 `{"key": "value"}` 或实体引用 `"Type:ID"`。**
- **掷骰**: 可以在**数值型**属性值中使用 `"XdY[+/-Z]"` 格式（例如 `hp="1d6+2"`, `damage="2d4"`, `quantity="1d3"`）。系统会在执行时计算结果。**位置属性不支持掷骰**。

#### 1.1 物品创建
@Create Item <item_id> (name="<显示名称>", [quantity=<数量或骰子>,] [location="<EntityType>:<所在位置ID>",] [其他自定义属性...])
- 默认值：`quantity=1`
- 示例：
@Create Item healing-potion (name="治疗药水", quantity=3, location="Character:hero", effect="增加7点HP", description="红色液体，散发着薄荷香气")
@Create Item rusty-key (name="生锈的钥匙", location="Place:old-chest", description="看起来能打开某个旧锁")
@Create Item forgotten-gem (name="遗忘宝石", location="Place:ruined-altar")
@Create Item goblin-loot (name="哥布林战利品", quantity="1d4", location="Place:goblin-camp", value=5)

#### 1.2 角色创建
@Create Character <char_id> (name="<角色名称>", [current_place="Place:<所在地点ID>",] [hp="<初始生命值或骰子>",] [其他自定义属性...])
- 示例：
@Create Character blacksmith-john (name="铁匠约翰", current_place="Place:village-square", description="胡子花白的老矮人，围裙上满是火星灼烧的痕迹", hp=25)
@Create Character goblin-scout (name="哥布林斥候", current_place="Place:dark-forest", hp="2d6", inventory=["rusty-dagger", "torn-pouch"])

#### 1.3 地点创建
@Create Place <place_id> (name="<地点名称>", [description="<描述>",] [exits={"<方向>": "<目标地点ID>", ...},] [其他自定义属性...])
- `exits` 是一个字典，定义出口。
- 示例：
@Create Place magic-library (name="魔法图书馆", description="高耸的书架直达穹顶，漂浮的蜡烛提供照明")
@Create Place crossroad (name="十字路口", description="一条泥泞的小路在此分岔。", exits={"north": "forest-path", "south": "village-gate", "west": "old-farm"})

### 2. 修改指令（@Modify）

#### 2.1 标准格式
@Modify <EntityType> <entity_id> (<属性名><操作符><值>, ...)
- 值可以是直接量、骰子表达式（用于数值属性）、列表、字典等。

#### 2.2 支持的操作符
| 操作符 | 适用类型        | 说明                                     | 示例                                     |
|--------|-----------------|------------------------------------------|----------------------------------------|
| =      | 所有            | 赋值 (覆盖原值)                          | `name="新名字"`, `hp=20`, `location="Place:new-place"` |
| +=     | 数值, 字符串, 列表 | 数值加, 字符串拼接, 列表添加**元素**       | `quantity+=1`, `description+=" (破损)"`, `inventory+=healing-potion-id` |
| -=     | 数值, 列表      | 数值减, 列表移除**第一个匹配项**             | `hp-=5`, `inventory-=rusty-key-id`       |
| +      | 数值, 字符串, 列表 | 数值加, 字符串拼接, **列表合并**           | `description=description + "!", inventory=inventory + ["gem1", "gem2"]` |
| -      | 数值, 列表, 属性  | 数值减, 列表移除**第一个匹配项**, **删除属性** | `hp=hp-10`, `contents=contents - "goblin-1"`, `owner=-` |
*注意：使用 `+`/`-` 进行属性修改时，通常需要引用属性自身，如 `hp=hp-10`。 `+=`/`-=` 更简洁。`owner=-` 会删除 owner 属性。*

#### 2.3 修改示例
@Modify Item player-sword (durability-=10, description+=" (剑刃出现裂纹)")
@Modify Character npc-merchant (attitude="friendly", gold+=50)
@Modify Place old-cave (description="洞穴深处传来滴水声。", contents+=bat-swarm-id) # 添加实体到地点
@Modify Character hero (inventory-=healing-potion-id) # 从角色移除物品
@Modify Place main-hall (is_lit=true) # 修改布尔值
@Modify Character boss-ogre (hp-="1d8+2") # 伤害掷骰

### 3. 转移指令（@Transfer）
（用于显式移动物品或角色，比 `@Modify location=` 更清晰）

#### 3.1 物品转移
@Transfer Item <item_id> (target="<EntityType>:<新位置ID>")
- 目标可以是地点 ID (`Place:ID`) 或角色 ID (`Character:ID`)。
- 示例：
@Transfer Item magic-ring (target="Place:treasure-chest")
@Transfer Item healing-potion (target="Character:player-char")

#### 3.2 角色移动
@Transfer Character <char_id> (target="Place:<新地点ID>")
- 示例：
@Transfer Character player-char (target="Place:dark-forest")

### 4. 销毁指令（@Destroy）
@Destroy <EntityType> <entity_id> ()
- 括号内无需参数。
- 示例：
@Destroy Item broken-arrow ()
@Destroy Character goblin-guard ()

## 三、重要提醒
*   **ID 唯一性**: 确保所有 `entity_id` 是唯一的。
*   **引用有效性**: 引用实体时（如 `location`, `target`, 列表中的 ID），尽量确保该 ID 已被 `@Create` 创建。如果引用的 ID 不存在，系统会尝试创建**占位符实体** (`Warning: Missing...`)，但这可能导致意外行为，请尽量避免。
*   **类型匹配**: 确保 `@Transfer` 和位置属性的 `EntityType` 正确。物品 (`Item`) 只能位于 `Place` 或 `Character` 中。角色 (`Character`) 只能位于 `Place` 中。
*   **堆叠**: 创建同名物品到同一容器时，系统会自动尝试增加现有物品的 `quantity` 而不是创建新物品实例。
*   **占位符处理**: 如果系统提示词中出现 `problematic_entities_report`，请优先使用 `@Modify` 修复这些占位符实体的 `name` 和其他属性，或使用 `@Destroy` 清理不再需要的占位符。

请根据用户的最新输入，继续故事，并使用指令更新世界状态。
"""


# --- YAML 格式化和包装函数 (修正版) ---
def format_as_yaml_block(data: Any, block_name: str = "yaml") -> str:
    """
    将 Python 对象转换为 YAML 字符串，并用指定的块名包装。
    例如：``yaml\nkey: value\n``
    如果 data 为空或转换失败，返回空字符串。
    """
    if not data:
        return ""

    try:
        yaml_string = yaml.dump(
            data, Dumper=yaml.Dumper, default_flow_style=False,
            allow_unicode=True, sort_keys=False, indent=2
        )
        yaml_string = re.sub(r'!!python/object:[^\s]+', '', yaml_string).strip()
        if not yaml_string: return ""

        # 返回包含块名包装的完整字符串
        return f"``{block_name}\n{yaml_string}\n``"

    except Exception as e:
        logging.error(f"将数据转换为 YAML 块 '{block_name}' 时出错: {e}", exc_info=True)
        return ""  # 失败时返回空字符串


# --- 生成问题实体报告的函数 (修正版) ---
def generate_problematic_entities_report(world: WorldState) -> str:
    """
    查找世界状态中所有未销毁的占位符实体，并生成包含 ``yaml ... `` 包装的报告字符串。
    """
    problematic = []
    warning_prefix = "Warning: Missing"
    entity_dicts = [world.items, world.characters, world.places]
    for entity_dict in entity_dicts:
        for entity_id, entity in entity_dict.items():
            if not entity.is_destroyed and entity.get_attribute('name', '').startswith(warning_prefix):
                problematic.append({
                    "entity_id": entity.entity_id,
                    "entity_type": entity.entity_type,
                    "current_name": entity.get_attribute('name', '')
                })

    # 使用修正后的包装函数
    report_yaml_block = format_as_yaml_block(problematic, block_name="yaml")  # 指定块名

    if not report_yaml_block:
        return ""  # 没有问题或格式化失败

    # 包装成提示信息，直接插入 report_yaml_block
    report_str = f"""
**--- 最高优先级！重要故障！系统自动创建的占位符实体等待填充或移除！ ---**
你之前可能引用了一些错误的实体ID（比如不小心把ID打错了，或者只顾着引用ID实际上忘了创建），系统为你自动创建了以下占位符。这些实体目前只有基础信息。
请检查这份列表，使用 `@Modify` 指令为它们设置**正确的名称**和其他必要属性，将它们融入你的叙事中。或者，如果你确认不再需要它们，可以使用 `@Destroy` 指令将其移除。
{report_yaml_block}
**请你立刻！！！马上！！！修复这个问题！！！其他的事情都可以不用管！！！**
**<redAlert><mostImportant>你必须先输出指令修复问题，然后再继续整个故事。</mostImportant></redAlert>**
**---------------------------------**"""
    return report_str.strip()


# --- 生成状态摘要的函数 (修正版) ---
def generate_state_summary(game_state: GameState) -> str:
    """
    根据 GameState 中的焦点和世界状态生成包含 ``yaml ... `` 包装的摘要字符串。
    如果无内容或失败，返回提示信息字符串。
    """
    world = game_state.world
    current_focus_ids = game_state.get_current_focus()

    summary_data: Dict[str, Any] = {"focused_entities": [], "world_slice": {"places": {}}}
    # --- 构建 summary_data 的逻辑 (保持不变) ---
    relevant_place_ids: Set[str] = set()
    focused_chars_no_place: Set[str] = set()
    active_focus_ids = []
    for fid in current_focus_ids:
        entity = world.find_entity(fid)
        if not entity: continue
        active_focus_ids.append(fid)
        if isinstance(entity, Place):
            relevant_place_ids.add(fid)
        elif isinstance(entity, Character):
            pid = entity.current_place
            if pid and world.find_entity(pid):
                relevant_place_ids.add(pid)
            else:
                focused_chars_no_place.add(fid)
        elif isinstance(entity, Item):
            loc = entity.location
            if loc:
                container = world.find_entity(loc)
                if container:
                    if isinstance(container, Place):
                        relevant_place_ids.add(loc)
                    elif isinstance(container, Character):
                        char_place_id = container.current_place
                        if char_place_id and world.find_entity(char_place_id):
                            relevant_place_ids.add(char_place_id)
                        else:
                            focused_chars_no_place.add(loc)
    summary_data["focused_entities"] = active_focus_ids
    places_slice = summary_data["world_slice"]["places"]
    processed_chars: Set[str] = set()
    for place_id in relevant_place_ids:
        place = world.find_entity(place_id)
        if not place or not isinstance(place, Place): continue
        place_data = place.get_all_attributes()
        place_data["characters"] = {}
        place_data["items"] = {}
        for content_id in place.contents:
            content = world.find_entity(content_id)
            if not content: continue
            if isinstance(content, Character):
                char_data = content.get_all_attributes()
                char_data["items"] = {}
                for item_id in content.has_items:
                    item = world.find_entity(item_id)
                    if item and isinstance(item, Item): char_data["items"][item_id] = item.get_all_attributes()
                place_data["characters"][content_id] = char_data
                processed_chars.add(content_id)
            elif isinstance(content, Item):
                place_data["items"][content_id] = content.get_all_attributes()
        places_slice[place_id] = place_data
    unplaced_chars_to_show = focused_chars_no_place - processed_chars
    if unplaced_chars_to_show:
        unplaced_data = {}
        for char_id in unplaced_chars_to_show:
            char = world.find_entity(char_id)
            if char and isinstance(char, Character):
                char_data = char.get_all_attributes()
                char_data["items"] = {}
                for item_id in char.has_items:
                    item = world.find_entity(item_id)
                    if item and isinstance(item, Item): char_data["items"][item_id] = item.get_all_attributes()
                unplaced_data[char_id] = char_data
        if unplaced_data: summary_data["world_slice"]["unplaced_focused_characters"] = unplaced_data
    if not places_slice: del summary_data["world_slice"]["places"]
    if "unplaced_focused_characters" in summary_data["world_slice"] and not summary_data["world_slice"][
        "unplaced_focused_characters"]: del summary_data["world_slice"]["unplaced_focused_characters"]
    if not summary_data["world_slice"]: del summary_data["world_slice"]
    if not summary_data["focused_entities"]: del summary_data["focused_entities"]
    # --- (构建 summary_data 逻辑结束) ---

    # 使用修正后的包装函数生成摘要块
    summary_yaml_block = format_as_yaml_block(summary_data, block_name="yaml")

    # 如果摘要块为空（数据为空或格式化失败），返回提示信息
    if not summary_yaml_block:
        return "当前焦点区域无可见内容或无焦点。"
    else:
        # 返回包含 ``yaml ... `` 包装的完整字符串
        return summary_yaml_block


# --- 函数用于生成提示词 (修正版) ---
def get_system_prompt(game_state: Optional[GameState] = None) -> str:
    """
    生成完整的系统提示词。
    """
    summary_placeholder = "摘要信息不可用或游戏未开始。"
    focus_description = "无特定焦点。"
    problematic_report_placeholder = ""  # 占位符匹配模板
    interaction_manual = INTERACTION_MANUAL  # 获取手册内容

    if game_state:
        # --- 生成 YAML 摘要块 ---
        try:
            # generate_state_summary 现在返回完整块或提示信息
            summary_placeholder = generate_state_summary(game_state)
        except Exception as e:
            logging.error(f"调用 generate_state_summary 时出错: {e}", exc_info=True)
            summary_placeholder = f"生成状态摘要时发生意外错误: {e}"

        # --- 生成焦点描述 (逻辑不变) ---
        current_focus_ids = game_state.get_current_focus()
        if current_focus_ids:
            focus_details = [f"{e.entity_type} '{e.name}' (ID: {e.entity_id})" if (
                e := game_state.find_entity(fid)) else f"无效焦点 (ID: {fid})" for fid in current_focus_ids]
            focus_description = ", ".join(focus_details) if focus_details else "无"
        else:
            focus_description = "无，请根据用户的描述判断意图。"

        # --- 生成问题实体报告块 ---
        try:
            # generate_problematic_entities_report 返回完整报告块或空字符串
            problematic_report_placeholder = generate_problematic_entities_report(game_state.world)
        except Exception as e:
            logging.error(f"调用 generate_problematic_entities_report 时出错: {e}", exc_info=True)
            problematic_report_placeholder = f"内部错误：生成问题实体报告时发生意外错误。"

    # 使用模板并替换所有占位符
    return SYSTEM_PROMPT_TEMPLATE.format(
        world_summary_placeholder=summary_placeholder,  # 包含 ``yaml...`` 或提示信息
        user_focus_description=focus_description,
        problematic_entities_report_placeholder=problematic_report_placeholder,  # 包含报告或空字符串
        interaction_manual_placeholder=interaction_manual  # 插入手册内容
    )


# --- 清理历史记录 (保持不变) ---
def clean_history_for_ai(history: List[Dict[str, str]]) -> List[Dict[str, str]]:
    """
    清理对话历史，移除所有 @Command 指令行，以便发送给 AI。
    """
    cleaned_history = []
    command_line_pattern = r"^\s*@.*$"
    for message in history:
        original_content = message.get("content", "")
        cleaned_content = re.sub(command_line_pattern, "", original_content, flags=re.MULTILINE).strip()
        cleaned_content = "\n".join(line for line in cleaned_content.splitlines() if line.strip())
        if cleaned_content or message.get("role") == "user":
            cleaned_history.append({"role": message["role"], "content": cleaned_content})
    return cleaned_history
