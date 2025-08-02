import { markRaw, type Component } from 'vue';

// --- 类型定义 ---
export interface DynamicAsset {
    pluginName: string;
    componentType: 'Vue' | 'WebComponent';
    scriptUrl: string;
    styleUrl?: string;
}

// --- 内部状态 ---
const vueComponentRegistry = new Map<string, Component>();
const loadedScriptUrls = new Set<string>();
const loadedStyleUrls = new Set<string>(); // <--- 新增：跟踪已加载的样式URL
let loadingPromise: Promise<any> | null = null;

/**
 * 动态加载脚本文件。
 * @param url - 脚本的URL。
 * @returns - 一个在脚本加载完成时解析的Promise。
 */
function loadScript(url: string): Promise<void> {
    if (loadedScriptUrls.has(url)) {
        return Promise.resolve();
    }
    return new Promise((resolve, reject) => {
        const script = document.createElement('script');
        script.src = url;
        script.async = true;
        script.type = 'rune';
        script.onload = () => {
            loadedScriptUrls.add(url);
            resolve();
        };
        script.onerror = () => reject(new Error(`无法加载脚本: ${url}`));
        document.head.appendChild(script);
    });
}

/**
 * 动态加载样式文件。
 * @param url - 样式的URL。
 * @returns - 一个在样式加载完成时解析的Promise。
 */
function loadStyle(url: string): Promise<void> {
    if (loadedStyleUrls.has(url)) { // <--- 关键修正：去重判断
        return Promise.resolve();
    }
    return new Promise((resolve, reject) => {
        const link = document.createElement('link');
        link.rel = 'stylesheet';
        link.href = url;
        link.onload = () => {
            loadedStyleUrls.add(url); // <--- 关键修正：标记为已加载
            resolve();
        };
        link.onerror = () => reject(new Error(`无法加载样式: ${url}`));
        document.head.appendChild(link);
    });
}

/**
 * 加载并注册所有动态组件资源。
 * @param assets - 从API获取的动态资源列表。
 */
export function loadAndRegisterPlugins(assets: DynamicAsset[]): Promise<void> {
    if (loadingPromise) return loadingPromise;

    const loadPromises: Promise<void>[] = []; // 用于收集本次需要加载的所有资源Promise

    assets.forEach(asset => {
        // 为每个 asset 创建一个 Promise 数组，包含其 JS 和 CSS 加载
        const assetSpecificPromises: Promise<void>[] = [];

        // 先加载样式 (如果存在且未加载过)
        if (asset.styleUrl && !loadedStyleUrls.has(asset.styleUrl)) { // 双重检查
            assetSpecificPromises.push(loadStyle(asset.styleUrl));
        }

        // 后加载脚本 (如果存在且未加载过)
        if (asset.scriptUrl && !loadedScriptUrls.has(asset.scriptUrl)) { // 双重检查
            assetSpecificPromises.push(loadScript(asset.scriptUrl).then(() => {
                if (asset.componentType === 'Vue') {
                    const pluginLib = (window as any)[asset.pluginName];
                    if (pluginLib?.default) {
                        for (const componentName in pluginLib.default) {
                            vueComponentRegistry.set(componentName, markRaw(pluginLib.default[componentName]));
                        }
                    }
                }
                // WebComponent 会在脚本执行时通过 customElements.define() 自行注册。
            }));
        }

        // 如果当前 asset 有新的 Promise 需要执行，就加入主加载队列
        if (assetSpecificPromises.length > 0) {
            loadPromises.push(Promise.all(assetSpecificPromises).then(() => {
                // console.log(`资产包 '${asset.pluginName}' 已加载完成。`);
            }));
        }
    });

    // 如果本次调用没有新的需要加载的资产，直接返回一个已解决的Promise
    if (loadPromises.length === 0) {
        return Promise.resolve();
    }

    loadingPromise = Promise.all(loadPromises).then(() => {
        console.log('所有动态插件已加载', {
            vueComponents: Array.from(vueComponentRegistry.keys()),
            loadedScripts: Array.from(loadedScriptUrls),
            loadedStyles: Array.from(loadedStyleUrls) // <--- 日志中也体现出来
        });
        // important: reset loadingPromise in the .then() and .catch()
    }).catch(error => {
        console.error('加载动态插件时发生错误:', error);
        return Promise.reject(error);
    }).finally(() => {
        loadingPromise = null; // 无论成功失败，都清空标记，允许下次再触发
    });

    return loadingPromise;
}

/**
 * 根据名称获取一个已注册的 Vue 插件组件。
 * @param name - 组件名（在后端RenderWithVueComponentAttribute中定义的ComponentName）。
 * @returns - Vue 组件定义或 undefined。
 */
export function getVuePluginComponent(name: string): Component | undefined {
    return vueComponentRegistry.get(name);
}