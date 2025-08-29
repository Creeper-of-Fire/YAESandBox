import {defineStore} from 'pinia';
import {computed} from 'vue';
import {type Item} from '#/types/models';
import {nanoid} from 'nanoid';
import {createPersistentState} from '#/composables/createPersistentState';

const STORAGE_KEY = 'era-lite-backpack';

interface BackpackState
{
    money: number;
    ownedItems: Item[];
}

export const useBackpackStore = defineStore(STORAGE_KEY, () =>
{
    // 使用一个 state 对象来统一管理持久化数据
    const {state, isReady} = createPersistentState<BackpackState>(STORAGE_KEY, {
        money: 1000,
        ownedItems: [],
    });

    // --- Getters for easier access ---
    const money = computed(() => state.value.money);
    const ownedItems = computed(() => state.value.ownedItems);

    // --- Actions ---
    function spendMoney(amount: number): boolean
    {
        if (state.value.money < amount)
        {
            return false;
        }
        state.value.money -= amount;
        return true;
    }

    function earnMoney(amount: number)
    {
        state.value.money += amount;
    }

    function addItem(item: Omit<Item, 'id'>)
    {
        const itemInstance = {...item, id: nanoid()};
        state.value.ownedItems.push(itemInstance);
    }

    function removeItem(itemInstanceId: string)
    {
        state.value.ownedItems = state.value.ownedItems.filter(item => item.id !== itemInstanceId);
    }

    // 允许更新背包中的某个物品，例如修改其描述或添加状态
    function updateItemInBackpack(updatedItem: Item)
    {
        const index = state.value.ownedItems.findIndex(item => item.id === updatedItem.id);
        if (index !== -1)
        {
            state.value.ownedItems[index] = updatedItem;
        }
    }

    return {
        money,
        ownedItems,
        isReady,
        spendMoney,
        earnMoney,
        addItem,
        removeItem,
        updateItemInBackpack
    };
});