import {onScopeDispose, type Ref, ref, watch} from 'vue';
import {type GlobalTheme} from 'naive-ui';
import {cloneDeep} from 'lodash-es';
import type {BuiltInGlobalTheme} from "naive-ui/lib/themes/interface";

// --- 颜色处理辅助函数 (无改动) ---

interface RGBA
{
    r: number;
    g: number;
    b: number;
    a: number;
}

interface LAB
{
    l: number;
    a: number;
    b: number;
    alpha: number;
}

/**
 * 递归地使两个主题对象的结构对称。
 * 如果一个键只在其中一个对象中存在，会使用另一个主题的 `bodyColor`
 * 作为回退值来填充缺失的键，从而确保每个属性都有明确的始末颜色。
 * @param source - 源主题对象部分
 * @param target - 目标主题对象部分
 * @param sourceFallbackColor - 源主题的回退颜色 (通常是 bodyColor)
 * @param targetFallbackColor - 目标主题的回退颜色 (通常是 bodyColor)
 */
function symmetrizeObjects(source: any, target: any, sourceFallbackColor: string, targetFallbackColor: string)
{
    const newSource = cloneDeep(source);
    const newTarget = cloneDeep(target);
    const allKeys = new Set([...Object.keys(newSource), ...Object.keys(newTarget)]);

    for (const key of allKeys)
    {
        const val1 = newSource[key];
        const val2 = newTarget[key];

        // 使用 == null 同时捕获 undefined 和 null
        if (val1 != null && typeof val1 === 'object' && val2 != null && typeof val2 === 'object')
        {
            const {symmetricSource, symmetricTarget} = symmetrizeObjects(val1, val2, sourceFallbackColor, targetFallbackColor);
            newSource[key] = symmetricSource;
            newTarget[key] = symmetricTarget;
        }
        else
        {
            if (val1 == null && val2 != null)
            {
                newSource[key] = isColorString(val2) ? sourceFallbackColor : val2;
            }
            if (val2 == null && val1 != null)
            {
                newTarget[key] = isColorString(val1) ? targetFallbackColor : val1;
            }
        }
    }
    return {symmetricSource: newSource, symmetricTarget: newTarget};
}


/**
 * 主对称化函数，从根级别开始处理。
 */
function createSymmetricThemes(source: GlobalTheme, target: GlobalTheme)
{
    // Naive UI 的 bodyColor 是最安全、最通用的回退色
    const sourceFallback = source.common?.bodyColor ?? '#FFFFFF';
    const targetFallback = target.common?.bodyColor ?? '#101010';

    return symmetrizeObjects(source, target, sourceFallback, targetFallback);
}

function lerp(start: number, end: number, t: number): number
{
    return start * (1 - t) + end * t;
}

function parseColor(str: string): RGBA | null
{
    if (typeof str !== 'string') return null;
    if (str.startsWith('#'))
    {
        const hex = str.slice(1);
        const bigint = parseInt(hex, 16);
        if (isNaN(bigint)) return null;
        const len = hex.length;
        if (len === 3 || len === 4)
        {
            return {
                r: ((bigint >> 8) & 0xF) * 17,
                g: ((bigint >> 4) & 0xF) * 17,
                b: (bigint & 0xF) * 17,
                a: len === 4 ? (((bigint >> 12) & 0xF) * 17) / 255 : 1,
            };
        }
        if (len === 6 || len === 8)
        {
            return {
                r: (bigint >> 16) & 255,
                g: (bigint >> 8) & 255,
                b: bigint & 255,
                a: len === 8 ? ((bigint >> 24) & 255) / 255 : 1,
            };
        }
    }
    const match = str.match(/rgba?\((\d+),\s*(\d+),\s*(\d+)(?:,\s*([\d.]+))?\)/);
    if (match)
    {
        return {
            r: parseInt(match[1], 10),
            g: parseInt(match[2], 10),
            b: parseInt(match[3], 10),
            a: match[4] !== undefined ? parseFloat(match[4]) : 1,
        };
    }
    return null;
}

/**
 * 将 sRGB 颜色转换为 CIELAB 颜色空间。
 * 这是实现感知均匀过渡的核心。
 */
function rgbToLab({r, g, b, a: alpha}: RGBA): LAB
{
    // 1. 标准化并进行逆伽马校正 (sRGB -> linear RGB)
    let R = r / 255, G = g / 255, B = b / 255;
    R = R > 0.04045 ? Math.pow((R + 0.055) / 1.055, 2.4) : R / 12.92;
    G = G > 0.04045 ? Math.pow((G + 0.055) / 1.055, 2.4) : G / 12.92;
    B = B > 0.04045 ? Math.pow((B + 0.055) / 1.055, 2.4) : B / 12.92;

    // 2. 转换为 XYZ 空间 (使用 D65 标准光源)
    let X = R * 0.4124564 + G * 0.3575761 + B * 0.1804375;
    let Y = R * 0.2126729 + G * 0.7151522 + B * 0.0721750;
    let Z = R * 0.0193339 + G * 0.1191920 + B * 0.9503041;

    // 3. 转换为 CIELAB 空间
    X /= 0.95047;
    Y /= 1.00000;
    Z /= 1.08883;

    const fn = (t: number) => t > 0.008856 ? Math.pow(t, 1 / 3) : (7.787 * t) + (16 / 116);
    X = fn(X);
    Y = fn(Y);
    Z = fn(Z);

    const L = (116 * Y) - 16;
    const a = 500 * (X - Y);
    const b_ = 200 * (Y - Z);

    return {l: L, a, b: b_, alpha};
}

/**
 * 将 CIELAB 颜色转换回 sRGB 颜色字符串。
 */
function labToRgbString({l, a, b, alpha}: LAB): string
{
    // 1. CIELAB -> XYZ
    let Y = (l + 16) / 116;
    let X = a / 500 + Y;
    let Z = Y - b / 200;

    const fn_inv = (t: number) => Math.pow(t, 3) > 0.008856 ? Math.pow(t, 3) : (t - 16 / 116) / 7.787;
    X = fn_inv(X) * 0.95047;
    Y = fn_inv(Y) * 1.00000;
    Z = fn_inv(Z) * 1.08883;

    // 2. XYZ -> linear RGB
    let R = X * 3.2404542 - Y * 1.5371385 - Z * 0.4985314;
    let G = X * -0.9692660 + Y * 1.8760108 + Z * 0.0415560;
    let B = X * 0.0556434 - Y * 0.2040259 + Z * 1.0572252;

    // 3. 伽马校正 (linear RGB -> sRGB)
    const gamma = (v: number) => v > 0.0031308 ? 1.055 * Math.pow(v, 1 / 2.4) - 0.055 : 12.92 * v;
    R = gamma(R);
    G = gamma(G);
    B = gamma(B);

    // 4. 裁剪并转换为 0-255 范围
    const clamp = (v: number) => Math.max(0, Math.min(1, v));
    const r_int = Math.round(clamp(R) * 255);
    const g_int = Math.round(clamp(G) * 255);
    const b_int = Math.round(clamp(B) * 255);

    return `rgba(${r_int}, ${g_int}, ${b_int}, ${alpha})`;
}

/**
 * 在 CIELAB 颜色空间中对两个颜色进行插值。
 */
function interpolateColorInLab(color1: string, color2: string, t: number): string
{
    const c1 = parseColor(color1);
    const c2 = parseColor(color2);

    if (!c1 || !c2) return t < 0.5 ? color1 : color2;

    const lab1 = rgbToLab(c1);
    const lab2 = rgbToLab(c2);

    const interpolatedLab: LAB = {
        l: lerp(lab1.l, lab2.l, t),
        a: lerp(lab1.a, lab2.a, t),
        b: lerp(lab1.b, lab2.b, t),
        alpha: lerp(lab1.alpha, lab2.alpha, t),
    };

    return labToRgbString(interpolatedLab);
}

// --- 经过重构的递归插值函数 ---

function isObject(value: any): value is object
{
    return value !== null && typeof value === 'object' && !Array.isArray(value);
}

function isColorString(value: any): value is string
{
    if (typeof value !== 'string') return false;
    return value.startsWith('#') || value.startsWith('rgb');
}

/**
 * 递归地插值两个主题对象。
 * 这是一个纯函数，它返回一个全新的对象，而不是修改现有对象。
 * @param source - 起始主题对象
 * @param target - 目标主题对象
 * @param t - 插值进度 (0 to 1)
 * @returns {any} - 计算出的新主题对象
 */
function interpolateTheme(source: any, target: any, t: number): any
{
    const result: { [key: string]: any } = {};
    for (const key in source)
    { // 只需遍历 source，因为结构已对称
        const val1 = source[key];
        const val2 = target[key];

        if (val1 !== null && typeof val1 === 'object')
        {
            result[key] = interpolateTheme(val1, val2, t);
        }
        else if (isColorString(val1) && isColorString(val2))
        {
            // 这里依然使用我们最好的 CIELAB 插值函数
            result[key] = interpolateColorInLab(val1, val2, t);
        }
        else
        {
            // 对于非颜色属性（数字、字符串等）
            result[key] = t < 0.5 ? val1 : val2;
        }
    }
    return result;
}

// --- ✨ 新增：临时禁用 CSS 过渡的 Composable ✨ ---

function useTransitionDisabler()
{
    let styleElement: HTMLStyleElement | null = null;
    const rule = `*, *::before, *::after { transition: none !important; }`;

    const disable = () =>
    {
        if (styleElement) return;
        styleElement = document.createElement('style');
        styleElement.textContent = rule;
        document.head.appendChild(styleElement);
    };

    const enable = () =>
    {
        if (!styleElement) return;
        document.head.removeChild(styleElement);
        styleElement = null;
    };

    // 确保在组件卸载时清理样式
    onScopeDispose(enable);

    return {disable, enable};
}

// --- 主 Composable ---

export function useThemeTransition(
    finalThemeName: Readonly<Ref<'light' | 'dark'>>,
    themes: { light: BuiltInGlobalTheme, dark: BuiltInGlobalTheme },
    duration = 1000 // 过渡持续时间 (ms)
)
{
    // 初始状态直接使用目标主题，避免闪烁
    const transitioningTheme = ref<GlobalTheme>(themes[finalThemeName.value]);
    let animationFrameId: number | null = null;

    watch(finalThemeName, (newThemeName) =>
    {
        if (animationFrameId)
        {
            cancelAnimationFrame(animationFrameId);
        }

        // 关键：sourceTheme 总是从当前正在显示的、可能不完整的过渡主题开始
        const sourceTheme = cloneDeep(transitioningTheme.value);
        const targetTheme = themes[newThemeName];
        let startTime: number | null = null;

        const {symmetricSource, symmetricTarget} = createSymmetricThemes(sourceTheme, targetTheme);

        // 缓动函数，使过渡更自然
        const easeInOutQuad = (t: number) => t < 0.5 ? 2 * t * t : -1 + (4 - 2 * t) * t;

        const animate = (timestamp: number) =>
        {
            if (!startTime) startTime = timestamp;
            const elapsedTime = timestamp - startTime;
            const progress = Math.min(elapsedTime / duration, 1);
            const easedProgress = easeInOutQuad(progress);

            // 使用重构后的、更健壮的插值函数
            const newTheme = interpolateTheme(symmetricSource, symmetricTarget, easedProgress);
            transitioningTheme.value = newTheme as GlobalTheme;


            if (progress < 1)
            {
                animationFrameId = requestAnimationFrame(animate);
            }
            else
            {
                // 确保最终状态是精确的目标主题，以清除任何插值误差
                transitioningTheme.value = cloneDeep(targetTheme); // 使用 cloneDeep 避免后续意外修改原始主题
                animationFrameId = null;
            }
        };

        animationFrameId = requestAnimationFrame(animate);
    }, {immediate: false}); // immediate: false 避免初始加载时就执行动画

    // 当主题名称变化时，立即更新一次，以防在动画未触发时主题不匹配
    watch(finalThemeName, (themeName) =>
    {
        if (!animationFrameId)
        {
            transitioningTheme.value = themes[themeName];
        }
    }, {immediate: true});


    return {transitioningTheme};
}