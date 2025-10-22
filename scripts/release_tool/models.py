from dataclasses import dataclass, field, asdict
from typing import List, Dict, Any


# 这是一个通用的清单条目
@dataclass
class ManifestEntry:
    id: str

    name: str
    notes: str  # 用于更新日志
    description: str  # 用于静态描述

    version: str
    hash: str
    url: str


# 核心组件的清单结构
@dataclass
class CoreManifest:
    components: List[ManifestEntry] = field(default_factory=list)

    def get_component_map(self) -> Dict[str, ManifestEntry]:
        """方便通过 ID 查找组件"""
        return {c.id: c for c in self.components}


# 顶层 Manifests 对象，它将完全替代 Dict[str, Any]
@dataclass
class Manifests:
    full: CoreManifest = field(default_factory=CoreManifest)
    slim: CoreManifest = field(default_factory=CoreManifest)
    plugins: List[ManifestEntry] = field(default_factory=list)

    @classmethod
    def from_dict(cls, data: Dict[str, Any]) -> 'Manifests':
        """从字典（如 json 加载的数据）创建 Manifests 对象"""
        if not data:
            return cls()

        # 为了向后兼容，当从可能没有 description 字段的旧 state.json 加载时，
        # 我们需要手动为每个条目设置默认值。
        def create_entry_with_default(entry_data: dict) -> ManifestEntry:
            entry_data.setdefault("description", "暂无描述。")
            entry_data.setdefault("notes", "暂无更新记录。")
            return ManifestEntry(**entry_data)

        full_components = [create_entry_with_default(c) for c in data.get("full", {}).get("components", [])]
        slim_components = [create_entry_with_default(c) for c in data.get("slim", {}).get("components", [])]
        plugins = [create_entry_with_default(p) for p in data.get("plugins", [])]

        return cls(
            full=CoreManifest(components=full_components),
            slim=CoreManifest(components=slim_components),
            plugins=plugins
        )

    def to_dict(self) -> Dict[str, Any]:
        """将 Manifests 对象序列化为可用于 JSON 存储的字典"""
        return asdict(self)


@dataclass
class Hashes:
    """定义了核心组件和插件的哈希值结构"""
    core: Dict[str, str] = field(default_factory=dict)
    plugins: Dict[str, str] = field(default_factory=dict)


@dataclass
class AppState:
    """完整描述 .release_state.json 的数据结构"""
    hashes: Hashes = field(default_factory=Hashes)
    manifests: Manifests = field(default_factory=Manifests)

    @classmethod
    def from_dict(cls, data: Dict[str, Any]) -> 'AppState':
        """从字典（如 json 加载的数据）创建 AppState 对象"""
        if not data:
            return cls()

        # 解析 hashes 字典
        raw_hashes = data.get("hashes", {})
        core_hashes = {k: v for k, v in raw_hashes.items() if k != 'plugins'}
        plugin_hashes = raw_hashes.get('plugins', {})

        return cls(
            hashes=Hashes(core=core_hashes, plugins=plugin_hashes),
            manifests=Manifests.from_dict(data.get("manifests", {}))
        )

    def to_dict(self) -> Dict[str, Any]:
        """将 AppState 对象序列化为可用于 JSON 存储的字典"""
        # 将 Hashes 对象重构回原始的 json 格式
        hashes_dict: Dict[str, Any] = self.hashes.core.copy()
        if self.hashes.plugins:
            hashes_dict["plugins"] = self.hashes.plugins

        return {
            "hashes": hashes_dict,
            "manifests": self.manifests.to_dict()
        }
