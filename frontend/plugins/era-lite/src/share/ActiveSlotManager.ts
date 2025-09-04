import { ref, readonly, computed, type Ref } from 'vue';
import type {SaveSlot} from "@yaesandbox-frontend/core-services/playerSave";

/**
 * 一个简单的响应式状态容器，用于在整个应用中跟踪当前激活的存档槽。
 */
export interface ActiveSlotManager {
    /** 当前激活的存档槽对象，或 null（表示未加载任何存档）。*/
    readonly activeSlot: Readonly<Ref<SaveSlot | null>>;

    /** 当前激活存档槽的 ID。*/
    readonly activeSlotId: Readonly<Ref<string | null>>;

    /** 当前激活存档槽的访问令牌。*/
    readonly activeSlotToken: Readonly<Ref<string | null>>;

    /**
     * 选择并激活一个存档槽，或通过传入 null 来卸载当前槽。
     * @param slot - 要激活的 SaveSlot 对象，或 null。
     */
    selectSlot(slot: SaveSlot | null): void;
}

/**
 * 创建一个 ActiveSlotManager 实例。
 */
export function createActiveSlotManager(): ActiveSlotManager {
    const _activeSlot = ref<SaveSlot | null>(null);

    function selectSlot(slot: SaveSlot | null): void {
        _activeSlot.value = slot;
        console.log(slot ? `存档槽已激活: "${slot.name}" (ID: ${slot.id})` : '所有存档槽已卸载。');
    }

    return {
        activeSlot: readonly(_activeSlot),
        activeSlotId: computed(() => _activeSlot.value?.id ?? null),
        activeSlotToken: computed(() => _activeSlot.value?.token ?? null),
        selectSlot,
    };
}