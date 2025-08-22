import {onScopeDispose, type Ref, ref, type UnwrapRef, watch} from 'vue';
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

// --- 主 Composable ---

// --- 过渡状态管理 ---
export const isTransitioning = ref(false);
const fallbackMask = ref({
    visible: false,
    color: '',
    opacity: 0,
});

let animationFrameId: number | null = null;

/**
 * 触发主题切换的过渡效果
 * @param event - 触发切换的鼠标点击事件
 * @param themes - 包含 light 和 dark 主题的对象
 * @param currentThemeName - 当前的主题名称 ('light' | 'dark')
 * @param targetThemeName - 目标主题名称 ('light' | 'dark')
 * @param duration - 动画时长 (ms)
 * @param onTransitionEnd - 在 DOM 更新和动画开始后执行的回调
 */
export function triggerThemeTransition(
    event: MouseEvent,
    themes: { light: GlobalTheme, dark: GlobalTheme },
    currentThemeName: 'light' | 'dark',
    targetThemeName: 'light' | 'dark',
    duration: number,
    onTransitionEnd: () => void
) {
    if (isTransitioning.value) return;

    const startX = event.clientX;
    const startY = event.clientY;
    // 动画开始前，立即强制整个页面的鼠标为手形
    document.documentElement.style.cursor = 'pointer';

    const cleanup = () => {
        isTransitioning.value = false;
        // 动画结束后，清除我们设置的强制样式，让鼠标恢复正常
        document.documentElement.style.cursor = '';
    };

    // --- 方案一：优先使用 View Transitions API ---
    // @ts-ignore - document.startViewTransition 可能不存在
    if (document.startViewTransition) {
        isTransitioning.value = true;

        // @ts-ignore
        const transition = document.startViewTransition(() => {
            onTransitionEnd();
        });

        transition.ready.then(() => {
            const endRadius = Math.hypot(
                Math.max(startX, window.innerWidth - startX),
                Math.max(startY, window.innerHeight - startY)
            );

            document.documentElement.animate(
                {
                    clipPath: [
                        `circle(0% at ${startX}px ${startY}px)`,
                        `circle(${endRadius}px at ${startX}px ${startY}px)`,
                    ],
                },
                {
                    duration,
                    easing: 'ease-in-out',
                    pseudoElement: '::view-transition-new(root)',
                }
            ).onfinish = cleanup;
        });
        return;
    }

    // --- 方案二：降级为颜色渐变遮罩 ---
    isTransitioning.value = true;

    const sourceColor = themes[currentThemeName].common?.bodyColor ?? '#ffffff';
    const targetColor = themes[targetThemeName].common?.bodyColor ?? '#1a1a1a';
    let startTime: number | null = null;

    // 缓动函数
    const easeInOutQuad = (t: number) => t < 0.5 ? 2 * t * t : -1 + (4 - 2 * t) * t;

    const animateMask = (timestamp: number) => {
        if (!startTime) {
            startTime = timestamp;
            // 在第一帧，立即切换底层主题
            onTransitionEnd();
            // 并显示一个完全不透明的、颜色为【旧主题背景色】的遮罩
            fallbackMask.value = { visible: true, color: sourceColor, opacity: 1 };
        }

        const elapsedTime = timestamp - startTime;
        const progress = Math.min(elapsedTime / duration, 1);
        const easedProgress = easeInOutQuad(progress);

        // a. 遮罩的颜色从【旧主题色】平滑过渡到【新主题色】
        fallbackMask.value.color = interpolateColorInLab(sourceColor, targetColor, easedProgress);

        // b. ✨ 核心修改：遮罩的透明度从 1 (完全不透明) 过渡到 0 (完全透明)
        fallbackMask.value.opacity = 1 - easedProgress;

        if (progress < 1) {
            animationFrameId = requestAnimationFrame(animateMask);
        } else {
            // 动画结束
            fallbackMask.value.visible = false;
            animationFrameId = null;
            cleanup();
        }
    };


    animationFrameId = requestAnimationFrame(animateMask);
}

// 导出一个可以在 App.vue 中使用的 Composable
export function useThemeFallbackMask() {
    onScopeDispose(() => {
        if (animationFrameId) {
            cancelAnimationFrame(animationFrameId);
        }
    });
    return { fallbackMask };
}