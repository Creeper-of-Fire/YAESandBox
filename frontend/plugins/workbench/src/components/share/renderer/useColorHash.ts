import { computed, inject, type Ref } from 'vue';
import { IsDarkThemeKey } from "@yaesandbox-frontend/core-services/injectKeys";
import ColorHash from "color-hash";

/**
 * 一个可组合函数，根据输入的字符串和当前主题（深/浅色）生成一个稳定的、美观的颜色。
 * @param keyRef - 一个包含字符串的响应式 Ref，此字符串将用于生成哈希颜色。
 * @returns 返回一个包含响应式 `color` Ref 的对象。
 */
export function useColorHash(keyRef: Ref<string>) {

    const isDarkThemeRaw = inject(IsDarkThemeKey);

    // 注入全局深色模式状态
    const isDarkTheme = computed(() => isDarkThemeRaw?.value);

    // 创建一个响应式的 ColorHash 实例。
    // 当主题变化时，这个计算属性会重新运行，返回一个为新主题配置好的新实例。
    const colorHashInstance = computed(() => {
        if (isDarkTheme.value) {
            // 深色模式配置：颜色更亮、饱和度稍低，以在深色背景上更柔和
            return new ColorHash({
                lightness: [0.70, 0.75, 0.80],
                saturation: [0.45, 0.55, 0.65],
                hash: 'bkdr'
            });
        } else {
            // 浅色模式配置：颜色更深、饱和度更高，以在浅色背景上更突出
            return new ColorHash({
                lightness: [0.50, 0.55, 0.60],
                saturation: [0.65, 0.75, 0.85],
                hash: 'bkdr'
            });
        }
    });

    // 计算最终的颜色值。
    // 它依赖于 keyRef 和 colorHashInstance，当任何一个变化时都会重新计算。
    const color = computed(() => {
        // 确保 keyRef.value 存在且不为空，避免 ColorHash 出错
        const key = keyRef.value || 'default-key';
        return colorHashInstance.value.hex(key);
    });

    return {
        /**
         * 计算出的十六进制颜色字符串 (e.g., "#aabbcc")
         */
        color
    };
}