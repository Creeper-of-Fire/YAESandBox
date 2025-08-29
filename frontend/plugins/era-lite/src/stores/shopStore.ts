import {defineStore} from 'pinia';
import {ref} from 'vue';
import {type Item} from '#/types/models';
import {nanoid} from 'nanoid';
import {createPersistentState} from "#/composables/createPersistentState.ts";
import {watchOnce} from "@vueuse/core";

const STORAGE_KEY = 'era-lite-shop';

export const useShopStore = defineStore(STORAGE_KEY, () =>
{
    const {state: itemsForSale, isReady} = createPersistentState<Item[]>(STORAGE_KEY, []);
    const isLoading = ref(false);

    // --- Actions ---


    /**
     * 向商店添加一个新物品。
     * @param itemData - 物品数据，无需包含 id。
     */
    function addItem(itemData: Omit<Item, 'id'>)
    {
        const newItem: Item = {
            ...itemData,
            id: nanoid(),
        };
        itemsForSale.value.unshift(newItem); // 添加到数组开头，方便查看
    }

    /**
     * 更新商店中的一个现有物品。
     * @param updatedItem - 包含完整 id 和更新后数据的物品对象。
     */
    function updateItem(updatedItem: Item)
    {
        const index = itemsForSale.value.findIndex(item => item.id === updatedItem.id);
        if (index !== -1)
        {
            itemsForSale.value[index] = updatedItem;
        }
    }

    /**
     * 从商店删除一个物品。
     * @param itemId - 要删除的物品的 id。
     */
    function deleteItem(itemId: string)
    {
        itemsForSale.value = itemsForSale.value.filter(item => item.id !== itemId);
    }


    /**
     * 填充商店商品。这是未来集成 AI 的入口点。
     * 现在我们用静态数据模拟，但保留了异步结构。
     */
    async function generateShopItems()
    {
        isLoading.value = true;
        console.log("正在生成/刷新商店商品...");

        // **未来**: 在这里调用你的工作流
        // const generatedItems = await workflowClient.run('generate-shop-items-workflow');
        // itemsForSale.value = generatedItems;

        // **现在**: 使用静态数据模拟
        await new Promise(resolve => setTimeout(resolve, 500)); // 模拟网络延迟
        itemsForSale.value = [
            {id: nanoid(), name: '荧光蘑菇', description: '在黑暗中散发着柔和的光芒。', price: 60},
            {id: nanoid(), name: '破译的密码盘', description: '似乎可以解开某种古老的锁。', price: 250},
            {id: nanoid(), name: '一瓶星尘', description: '握在手中感觉暖洋洋的。', price: 400},
        ];

        console.log("商店商品已更新。");
        isLoading.value = false;
    }

    // --- Initialization Logic ---
    // 仅在首次加载完成，且商店为空时，生成初始商品
    watchOnce(isReady, () =>
    {
        if (isReady.value && itemsForSale.value.length === 0)
        {
            generateShopItems();
        }
    });


    return {
        itemsForSale,
        isLoading,
        generateShopItems,
        addItem,
        updateItem,
        deleteItem
    };
});