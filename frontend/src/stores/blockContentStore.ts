import {defineStore} from 'pinia';
import type {BlockDetailDto, UpdateBlockDetailsDto} from '@/types/generated/api';
import {BlocksService, BlockStatusCode} from '@/types/generated/api'; // 引入用于检查状态
import { useBlockStatusStore } from './blockStatusStore';

interface BlockContentState {
    blocks: Record<string, BlockDetailDto>; // ID -> Detail DTO 缓存
    isLoading: Set<string>;
}

const defaultState: BlockContentState = {
    blocks: {},
    isLoading: new Set(),
};

export const useBlockContentStore = defineStore('blockContent', {
    state: (): BlockContentState => ({...defaultState}),

    getters: {
        /** 获取指定 ID 的 Block 详细信息 DTO */
        getBlockById(state): (id: string) => BlockDetailDto | undefined {
            return (id: string) => state.blocks[id];
        },
    },

    actions: {
        /**
         * 获取指定 Block 的详细信息并更新缓存，同时同步状态码到 BlockStatusStore。
         * @param blockId 要获取的 Block ID。
         * @param forceRefetch 是否强制重新获取，即使缓存中已存在。
         */
        async fetchBlockDetails(blockId: string, forceRefetch: boolean = false) {
            // 引入 BlockStatusStore
            const statusStore = useBlockStatusStore();

            if (this.isLoading.has(blockId)) return;
            // 仅当非强制且缓存存在，并且状态缓存也存在时才跳过 (确保状态也被初始化了)
            // if (!forceRefetch && this.blocks[blockId] && statusStore.getBlockStatus(blockId)) return;
            // 简化：如果缓存存在，通常状态也应该存在或即将通过此方法设置，所以只检查缓存
            if (!forceRefetch && this.blocks[blockId]) return;


            this.isLoading.add(blockId);
            console.log(`BlockContentStore: 开始获取 Block ${blockId} 详情 (强制: ${forceRefetch})...`);
            try {
                const blockData = await BlocksService.getApiBlocks1({ blockId });
                console.log(`BlockContentStore: Block ${blockId} 详情获取成功，状态码: ${blockData.statusCode}`);

                // 检查获取到的 Block 是否有效
                if (blockData.statusCode === BlockStatusCode.DELETED || blockData.statusCode === BlockStatusCode.NOT_FOUND) {
                    console.warn(`BlockContentStore: 获取到 Block ${blockId} 状态为 ${blockData.statusCode}，从内容缓存移除。`);
                    delete this.blocks[blockId];
                    // 同步状态到 StatusStore (标记为 Deleted 或 NotFound)
                    statusStore.setBlockStatusDirectly(blockId, blockData.statusCode); // <--- 新增方法调用
                } else {
                    // 更新内容缓存
                    this.blocks[blockId] = blockData;
                    console.log(`BlockContentStore: Block ${blockId} 详情已更新缓存。`);

                    // *** 关键：将获取到的状态码同步到 BlockStatusStore ***
                    statusStore.setBlockStatusDirectly(blockId, blockData.statusCode ?? BlockStatusCode.IDLE); // <--- 新增方法调用 (提供默认 Idle 以防万一)
                    console.log(`BlockContentStore: 已同步 Block ${blockId} 状态 (${blockData.statusCode ?? 'Idle'}) 到 StatusStore。`);
                }
            } catch (error: any) {
                console.error(`BlockContentStore: 获取 Block ${blockId} 详情失败`, error);
                if ((error as any).status === 404) {
                    console.warn(`BlockContentStore: 获取 Block ${blockId} 详情失败 (404)，从内容缓存移除。`);
                    delete this.blocks[blockId];
                    // 同步状态为 NotFound
                    statusStore.setBlockStatusDirectly(blockId, BlockStatusCode.NOT_FOUND); // <--- 新增方法调用
                }
                // 其他错误，暂时保留旧缓存（如果有），状态也可能保持不变或未知
            } finally {
                this.isLoading.delete(blockId);
            }
        },

        /**
         * [由 BlockStatusStore 调用] 更新指定 Block 的内容。
         * @param blockId 要更新的 Block ID。
         * @param newContent 新的内容片段或完整内容。
         * @param updateMode 更新模式 ('Incremental' 或 'FullSnapshot')。
         */
        updateBlockContent(blockId: string, newContent: string | null, updateMode: string) {
            if (this.blocks[blockId]) {
                if (updateMode === 'Incremental') {
                    this.blocks[blockId].blockContent = (this.blocks[blockId].blockContent ?? "") + (newContent ?? "");
                } else { // FullSnapshot or unknown (default to full)
                    this.blocks[blockId].blockContent = newContent ?? "";
                }
                // console.log(`BlockContentStore: Block ${blockId} 内容已更新 (模式: ${updateMode})`);
            } else {
                // 收到内容更新，但 Block 详情不在缓存中，这可能意味着状态不同步
                // 可能是 BlockStatusUpdate 比 fetchBlockDetails 先到
                console.warn(`BlockContentStore: 尝试更新未知 Block ${blockId} 的内容。可能丢失更新。尝试获取其详情...`);
                // 触发一次详情获取，希望能补上
                this.fetchBlockDetails(blockId);
                // 注意：这次更新可能丢失，因为 fetch 是异步的。更好的做法是 BlockStatusStore 在处理 DisplayUpdate 时检查 contentStore 中是否存在 block，如果不存在则先 fetch 再更新。
            }
        },

        /**
         * 更新 Block 的元数据 (例如，通过编辑器保存后)。
         * 注意：这通常由 API 调用直接完成，然后通过 `BlockUpdateSignal` 触发详情刷新。
         * 但如果前端需要乐观更新，可以提供此方法。
         */
        // updateBlockMetadata(blockId: string, metadataUpdates: Record<string, string | null>) { ... }

        /**
         * 通过 API 修改 Block 的内容或元数据。
         * @param blockId Block ID。
         * @param updates 包含 content 和/或 metadataUpdates 的对象。
         */
        async patchBlockDetails(blockId: string, updates: UpdateBlockDetailsDto) {
            const statusStore = useBlockStatusStore();
            if (statusStore.isLoadingAction) return; // 检查全局操作状态
            // 检查 Block 状态是否允许修改 (通常是 Idle)
            const currentStatus = statusStore.getBlockStatus(blockId);
            if (currentStatus !== BlockStatusCode.IDLE) {
                console.warn(`BlockContentStore: Block ${blockId} 状态为 ${currentStatus}，不允许修改。`);
                alert(`Block (${blockId}) 当前状态 (${currentStatus}) 不允许修改。`);
                return;
            }

            statusStore.setLoadingAction(true, `patch_${blockId}`); // 标记开始操作
            try {
                await BlocksService.patchApiBlocks({blockId, requestBody: updates});
                console.log(`BlockContentStore: Block ${blockId} 的 PATCH 请求已发送。`);
                // 成功后，后端会发送 BlockUpdateSignal，前端会通过 handleBlockUpdateSignal 刷新详情
                // 也可以选择在这里进行乐观更新
                if (updates.content !== undefined && this.blocks[blockId]) {
                    this.blocks[blockId].blockContent = updates.content;
                }
                if (updates.metadataUpdates && this.blocks[blockId]) {
                    if (!this.blocks[blockId].metadata) this.blocks[blockId].metadata = {};
                    for (const key in updates.metadataUpdates) {
                        const value = updates.metadataUpdates[key];
                        if (value === null) {
                            delete this.blocks[blockId].metadata![key];
                        } else {
                            this.blocks[blockId].metadata![key] = value;
                        }
                    }
                }
            } catch (error) {
                console.error(`BlockContentStore: 更新 Block ${blockId} 失败`, error);
                alert(`更新 Block (${blockId}) 失败: ${error instanceof Error ? error.message : error}`);
            } finally {
                statusStore.setLoadingAction(false, `patch_${blockId}`); // 标记结束操作
            }

        },

        /**
         * 清除所有缓存的 Block 详情 (用于加载新存档前)。
         */
        clearAllBlocks() {
            this.blocks = {};
            this.isLoading.clear();
            console.log("BlockContentStore: 缓存已清除。");
        },
    }
});