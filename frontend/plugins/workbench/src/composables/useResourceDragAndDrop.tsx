// useResourceDragAndDrop.ts
import {type CSSProperties, type FunctionalComponent, ref, type Ref} from 'vue';
import type {ConfigType} from "@yaesandbox-frontend/core-services/types";
import type {GlobalEditSession} from '#/services/GlobalEditSession';
import {useThemeVars} from "naive-ui";

// 定义数据契约 (Payload)
export interface DragPayload
{
    type: ConfigType;
    storeId: string;
}

// 定义常量，避免魔法字符串
const MIME_TYPE_PREFIX = 'application/vnd.workbench.item.';
const PLAIN_TEXT_MIME_TYPE = 'text/plain';

// 内部辅助函数
const typeHierarchy: Record<ConfigType, number> = {
    workflow: 3,
    tuum: 2,
    rune: 1,
};

function getDraggedItemType(event: DragEvent): ConfigType | null
{
    for (const type of event.dataTransfer?.types ?? [])
    {
        const match = type.match(/^application\/vnd\.workbench\.item\.(workflow|tuum|rune)$/);
        if (match)
        {
            return match[1] as ConfigType;
        }
    }
    return null;
}

// --- 导出给 Provider 使用的函数 ---

/**
 * 创建一个用于 vue-draggable-plus 的 setData 回调。
 * 它从 DOM 元素读取元数据，并将其正确格式化后放入 DataTransfer 对象。
 * @returns 一个 setData 回调函数
 */
export function useResourceDragProvider()
{
    const setDataHandler = (dataTransfer: DataTransfer, dragEl: HTMLElement) =>
    {
        const type = dragEl.dataset.dragType as ConfigType | undefined;
        const storeId = dragEl.dataset.dragId;

        if (type && storeId)
        {
            const payload: DragPayload = {type, storeId};
            // 存储序列化的 JSON 数据，用于 drop 时解析
            dataTransfer.setData(PLAIN_TEXT_MIME_TYPE, JSON.stringify(payload));
            // 存储一个自定义类型，用于在 dragenter 时快速识别拖拽物类型
            dataTransfer.setData(`${MIME_TYPE_PREFIX}${type}`, storeId);
            dataTransfer.effectAllowed = 'copy';
        }
    };

    return {setDataHandler};
}


// --- 导出给 Consumer 使用的函数 ---

export interface DropZoneConfig
{
    /** 当前激活的会话，用于进行拖拽兼容性检查 */
    session: Ref<GlobalEditSession | null | undefined>;
    /** 当一个有效的项目被放置时触发的回调 */
    onDrop: (payload: DragPayload) => void;
}

/**
 * 为一个组件提供拖拽放置区 (Drop Zone) 的功能。
 * @param config - 放置区的配置对象
 */
export function useResourceDropZone(config: DropZoneConfig)
{
    const {session, onDrop} = config;
    // 内部共享状态
    const isDragOver = ref(false);

    const dragEnterHandler = (event: DragEvent) =>
    {
        const draggedType = getDraggedItemType(event);
        if (!draggedType) return;

        // 如果没有会话，任何资源都可以拖入以开启新会话
        if (!session.value)
        {
            isDragOver.value = true;
            return;
        }

        // 核心逻辑：只有当拖拽物的层级 >= 当前会话的层级时，才允许替换
        const draggedLevel = typeHierarchy[draggedType];
        const currentLevel = typeHierarchy[session.value.type];
        if (draggedLevel >= currentLevel)
        {
            isDragOver.value = true;
        }
    };

    const dragLeaveHandler = () =>
    {
        isDragOver.value = false;
    };

    const dropHandler = (event: DragEvent) =>
    {
        event.preventDefault(); // 防止浏览器执行默认行为。

        isDragOver.value = false;
        if (event.dataTransfer)
        {
            try
            {
                const dataString = event.dataTransfer.getData(PLAIN_TEXT_MIME_TYPE);
                if (dataString)
                {
                    const payload = JSON.parse(dataString) as DragPayload;
                    onDrop(payload);
                }
            } catch (e)
            {
                console.error("解析拖拽数据失败:", e);
            }
        }
    };

    // --- 定义 DropZoneContainer 组件 ---
    const DropZoneContainer: FunctionalComponent = (props, {slots, attrs}) =>
    {
        return (
            <div
                {...attrs} // 透传 class, id 等
                onDragenter={dragEnterHandler}
                onDragover={(e: DragEvent) => e.preventDefault()}
            >
                {slots.default ? slots.default() : null}
            </div>
        );
    }

    // --- 定义 DropZoneOverlay 组件 ---
    const DropZoneOverlay: FunctionalComponent = (props, {slots, attrs}) =>
    {
        // 使用 CSS-in-JS 定义样式
        const themeVars = useThemeVars();
        const overlayStyle: CSSProperties = {
            position: 'absolute',
            top: 0,
            left: 0,
            width: '100%',
            height: '100%',
            border: `2px dashed ${themeVars.value.primaryColor}`,
            borderRadius: '6px',
            zIndex: 10,
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            boxSizing: 'border-box',

            // ✨ 视觉增强
            backgroundColor: `rgba(${hexToRgb(themeVars.value.primaryColorSuppl)}, 0.2)`,
            backdropFilter: 'blur(1px)',

            // ✨ 平滑过渡效果
            opacity: isDragOver.value ? 1 : 0,
            transform: isDragOver.value ? 'scale(1)' : 'scale(1.02)', // 微小的缩放效果
            transition: 'opacity 0.2s ease, transform 0.2s ease',

            // ✨ 控制交互性：不可见时不能捕获鼠标事件
            pointerEvents: isDragOver.value ? 'auto' : 'none',
        };

        // 渲染逻辑：只有 isDragOver 为 true 时才渲染
        if (!isDragOver.value)
        {
            return null;
        }

        return (
            <div
                {...attrs}
                style={overlayStyle}
                onDragleave={dragLeaveHandler}
                onDrop={dropHandler}
                // 在覆盖层上也需要阻止 dragover，否则 drop 不会触发
                onDragover={(e: DragEvent) => e.preventDefault()}
            >
                {slots.default ? slots.default() : null}
            </div>
        );
    };

    return {
        DropZoneContainer,
        DropZoneOverlay,
    };
}

// 简单的辅助函数，将 hex 颜色转为 rgb 值
function hexToRgb(hex: string): string {
    const result = /^#?([a-f\d]{2})([a-f\d]{2})([a-f\d]{2})$/i.exec(hex);
    return result
        ? `${parseInt(result[1], 16)}, ${parseInt(result[2], 16)}, ${parseInt(result[3], 16)}`
        : '0, 0, 0';
}