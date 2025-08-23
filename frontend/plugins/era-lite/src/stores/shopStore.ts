import { defineStore } from 'pinia';
import {ref, toRaw, watch} from 'vue';
import {type Item } from '#/types/models';
import { nanoid } from 'nanoid';
import localforage from 'localforage';

const STORAGE_KEY = 'era-lite-shop';

export const useShopStore = defineStore(STORAGE_KEY, () => {
    const itemsForSale = ref<Item[]>([]);
    const isLoading = ref(false);

    // --- Actions ---

    /**
     * 填充商店商品。这是未来集成 AI 的入口点。
     * 现在我们用静态数据模拟，但保留了异步结构。
     */
    async function generateShopItems() {
        isLoading.value = true;
        console.log("正在生成/刷新商店商品...");

        // **未来**: 在这里调用你的工作流
        // const generatedItems = await workflowClient.run('generate-shop-items-workflow');
        // itemsForSale.value = generatedItems;

        // **现在**: 使用静态数据模拟
        await new Promise(resolve => setTimeout(resolve, 500)); // 模拟网络延迟
        itemsForSale.value = [
            { id: nanoid(), name: '荧光蘑菇', description: '在黑暗中散发着柔和的光芒。', price: 60 },
            { id: nanoid(), name: '破译的密码盘', description: '似乎可以解开某种古老的锁。', price: 250 },
            { id: nanoid(), name: '一瓶星尘', description: '握在手中感觉暖洋洋的。', price: 400 },
        ];

        console.log("商店商品已更新。");
        isLoading.value = false;
    }

    // --- Persistence ---
    // 从 IndexedDB 加载初始状态
    localforage.getItem<Item[]>(STORAGE_KEY).then(savedItems => {
        if (savedItems && savedItems.length > 0) {
            itemsForSale.value = savedItems;
        } else {
            // 如果没有缓存，则自动生成第一批商品
            generateShopItems();
        }
    });

    // 监听变化并持久化
    watch(itemsForSale, (newItems) => {
        localforage.setItem(STORAGE_KEY, toRaw(newItems));
    }, { deep: true });


    return { itemsForSale, isLoading, generateShopItems };
});