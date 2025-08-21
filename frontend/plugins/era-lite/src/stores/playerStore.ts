import { defineStore } from 'pinia';
import { ref, computed, watch } from 'vue';
import { type Item } from '#/types/models';
import { nanoid } from 'nanoid';
import localforage from 'localforage';

const STORAGE_KEY = 'era-lite-player';

// 定义持久化数据的结构
interface PlayerState {
    money: number;
    ownedItems: Item[];
}

export const usePlayerStore = defineStore(STORAGE_KEY, () => {
    // --- State ---
    const money = ref(1000);
    const ownedItems = ref<Item[]>([]);

    // --- Actions ---
    function spendMoney(amount: number): boolean {
        if (money.value < amount) {
            console.warn('Attempted to spend more money than available.');
            return false;
        }
        money.value -= amount;
        return true;
    }

    function addItem(item: Item) {
        // 为添加到背包的每个物品实例创建一个唯一的ID，以防玩家购买多个同名物品
        const itemInstance = { ...item, id: nanoid() };
        ownedItems.value.push(itemInstance);
    }

    // --- Persistence ---
    const stateToPersist = computed((): PlayerState => ({
        money: money.value,
        ownedItems: ownedItems.value,
    }));

    localforage.getItem<PlayerState>(STORAGE_KEY).then(savedState => {
        if (savedState) {
            money.value = savedState.money;
            ownedItems.value = savedState.ownedItems;
        }
    });

    watch(stateToPersist, (newState) => {
        localforage.setItem(STORAGE_KEY, newState);
    }, { deep: true });

    return { money, ownedItems, spendMoney, addItem };
});