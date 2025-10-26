import {computed, type Ref, watch, ref, inject} from 'vue';
import {useModal} from 'naive-ui';
import {
    type InputMatchingMode,
    type MatchingOptions,
    type TagMatchingMode,
    useFilteredWorkflowSelector,
    type WorkflowFilter
} from './useFilteredWorkflowSelector';
import {useScopedStorage} from '../../composables/useScopedStorage';
import WorkflowSelectorPanel from './WorkflowSelectorPanel.vue';
import {type IWorkflowAnalysisProvider, WorkflowAnalysisProviderKey} from "../../inject-key/WorkflowAnalysisProvider.ts";
import type {WorkflowValidationReport} from "../../types";

/**
 * 【新增】一个使用命令式API来打开工作流选择模态框的 Composable。
 * 它封装了UI交互，同时依赖纯粹的 useFilteredWorkflowSelector 进行逻辑处理。
 *
 * @param storageKey 用于持久化用户选择和UI偏好的唯一键。
 * @param filter 一个响应式的筛选条件对象 Ref。
 * @param expectedOutputs 一个响应式的、纯展示用的期望输出列表 Ref。
 * @param initialOptions 可选的、用于首次加载时的默认匹配模式。
 */
export function useFilteredWorkflowSelectorModal(
    storageKey: string,
    filter: Ref<WorkflowFilter>,
    expectedOutputs: Ref<string[] | null>,
    initialOptions?: Partial<MatchingOptions>)
{

    const modal = useModal();

    // 1. 持久化用户对匹配模式的选择
    const inputMatchingMode = useScopedStorage<InputMatchingMode>(`${storageKey}-input-mode`, initialOptions?.inputs ?? 'normal');
    const tagMatchingMode = useScopedStorage<TagMatchingMode>(`${storageKey}-tag-mode`, initialOptions?.tags ?? 'prefer');

    const matchingOptions = computed<MatchingOptions>(() => ({
        inputs: inputMatchingMode.value,
        tags: tagMatchingMode.value,
    }));

    // 2. 实例化底层的、纯粹的逻辑 Composable
    const selectorLogic = useFilteredWorkflowSelector(storageKey, filter, matchingOptions);

    // 3. 注入分析服务并创建状态来存储分析报告
    const analysisProvider = inject<IWorkflowAnalysisProvider>(WorkflowAnalysisProviderKey);
    const analysisReports = ref<Map<string, WorkflowValidationReport | null>>(new Map());

    // 4. 监听筛选结果的变化，并自动为列表中的工作流获取分析报告
    watch(selectorLogic.filteredAndSortedWorkflows, async (workflows) =>
    {
        if (!analysisProvider) return;

        // 创建一个Promise数组来并行获取所有报告
        const reportPromises = workflows.map(async (wf) =>
        {
            // 如果已经有报告或正在获取，则跳过
            if (analysisReports.value.has(wf.id))
            {
                return;
            }
            // 先设置一个null值表示正在加载
            analysisReports.value.set(wf.id, null);
            try
            {
                const report = await analysisProvider.getReport(wf.resource);
                analysisReports.value.set(wf.id, report || null);
            } catch (error)
            {
                console.error(`获取工作流 ${wf.resource.name} 的分析报告失败:`, error);
                // 可以设置一个错误状态，但这里简单地保持null
                analysisReports.value.set(wf.id, null);
            }
        });

        await Promise.all(reportPromises);

    }, {immediate: true}); // immediate: true 确保首次加载时就触发

    /**
     * 打开选择模态框。
     * @returns 一个 Promise，当用户选择一个工作流时 resolve(id)，当用户关闭模态框时 reject()。
     */
    function open(): Promise<string>
    {
        return new Promise((resolve, reject) =>
        {
            const modalInstance = modal.create({
                title: '选择一个工作流',
                preset: 'card',
                style: {
                    width: '600px',
                },
                content: () => (
                    <WorkflowSelectorPanel
                        // Props: 传递响应式数据
                        workflows={selectorLogic.filteredAndSortedWorkflows.value}
                        filter={filter.value}

                        expectedOutputs={expectedOutputs.value}
                        analysisReports={analysisReports.value}

                        inputMode={inputMatchingMode.value}
                        onUpdate:inputMode={value => (inputMatchingMode.value = value)}

                        tagMode={tagMatchingMode.value}
                        onUpdate:tagMode={value => (tagMatchingMode.value = value)}

                        // 自定义事件
                        onSelect={(id: string) =>
                        {
                            selectorLogic.selectWorkflow(id);
                            modalInstance.destroy();
                            resolve(id);
                        }}
                    />
                ),
                // 当用户通过点击遮罩、按ESC或右上角关闭按钮时
                onClose: () =>
                {
                    reject(new Error('用户取消选择'));
                    return true; // 返回true允许关闭
                },
                onNegativeClick: () =>
                {
                    reject(new Error('用户取消选择'));
                    return true;
                }
            });
        });
    }

    return {
        ...selectorLogic, // 暴露所有底层逻辑的状态和方法
        openSelectorModal: open, // 暴露打开模态框的方法
    };
}