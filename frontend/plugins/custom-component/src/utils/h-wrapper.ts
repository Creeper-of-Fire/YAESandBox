import {h as originalH, Fragment, type VNode} from 'vue';

/**
 * 这是一个智能的、包装过的 `h` 函数，用于解决 Sucrase + Vue 3
 * 导致的 "Non-function value encountered for default slot" 警告，
 * 同时避免在原生 HTML 标签上应用错误的逻辑。
 *
 * @param type 组件类型或 HTML 标签名
 * @param props 属性
 * @param children 子元素
 * @returns VNode
 */
export function h(type: any, props?: any, ...children: any[]): VNode {
    // 启发式检测：Vue 组件的 'type' 通常是对象或函数，
    // 而原生 HTML 标签的 'type' 是一个字符串。
    const isComponent = typeof type !== 'string';

    // 只对组件且其拥有子节点时，才应用插槽包装逻辑
    if (isComponent && children.length > 0) {
        // 使用 .flat() 来处理由 .map() 产生的嵌套数组，使逻辑更健壮
        const flatChildren = children.flat();

        // 这是向组件传递插槽的最标准、最健壮的方式：一个 slot 对象。
        return originalH(type, props, {
            default: () => flatChildren
        });
    }

    // 对于原生 HTML 标签，或者没有子节点的组件，
    // 使用原始的 h 函数行为，直接传递子节点。
    // 这里的 ...children 至关重要。
    return originalH(type, props, ...children);
}

// 确保 Fragment 也被正确导出
export { Fragment };