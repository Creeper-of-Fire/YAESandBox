import {type Component, markRaw} from 'vue';
import {getAbsoluteUrl} from '@yaesandbox-frontend/core-services';

// --- 类型定义 ---
export interface DynamicAsset
{
    pluginId: string;
    componentType: 'Vue' | 'WebComponent';
    scriptUrl: string;
    styleUrl?: string;
}

// --- 内部状态 ---
const vueComponentRegistry = new Map<string, Component>();
const loadedScriptUrls = new Set<string>();
const loadedStyleUrls = new Set<string>();
let loadingPromise: Promise<any> | null = null;

/**
 * 动态加载脚本文件。
 * @param url - 脚本的【处理后URL】。
 * @returns - 一个在脚本加载完成时解析的Promise。
 */
function loadScript(url: string): Promise<void>
{
    if (loadedScriptUrls.has(url))
    {
        return Promise.resolve();
    }
    return new Promise((resolve, reject) =>
    {
        const script = document.createElement('script');
        script.src = url;
        script.async = true;
        script.onload = () =>
        {
            loadedScriptUrls.add(url);
            resolve();
        };
        script.onerror = () => reject(new Error(`无法加载脚本: ${url}`));
        document.head.appendChild(script);
    });
}

/**
 * 动态加载样式文件。
 * @param url - 样式的【处理后URL】。
 * @returns - 一个在样式加载完成时解析的Promise。
 */
function loadStyle(url: string): Promise<void>
{
    if (loadedStyleUrls.has(url))
    { // <--- 关键修正：去重判断
        return Promise.resolve();
    }
    return new Promise((resolve, reject) =>
    {
        const link = document.createElement('link');
        link.rel = 'stylesheet';
        link.href = url;
        link.onload = () =>
        {
            loadedStyleUrls.add(url); // <--- 关键修正：标记为已加载
            resolve();
        };
        link.onerror = () => reject(new Error(`无法加载样式: ${url}`));
        document.head.appendChild(link);
    });
}

/**
 * 异步加载和注册所有动态组件资源 (async/await 版本)
 * @param assets - 从API获取的动态资源列表。
 */
async function doLoadAndRegister(assets: DynamicAsset[]): Promise<void>
{
    const promisesToAwait: Promise<void>[] = [];

    // 第一轮：构建绝对URL，收集所有需要加载的资源，并发起请求
    for (const asset of assets)
    {

        // 加载样式
        if (asset.styleUrl)
        {
            const absoluteUrl = getAbsoluteUrl(asset.styleUrl);
            if (!loadedStyleUrls.has(absoluteUrl))
            {
                promisesToAwait.push(loadStyle(absoluteUrl));
            }
        }
        // 加载脚本
        if (asset.scriptUrl)
        {
            const absoluteUrl = getAbsoluteUrl(asset.scriptUrl);
            if (!loadedScriptUrls.has(absoluteUrl))
            {
                promisesToAwait.push(loadScript(absoluteUrl));
            }
        }
    }

    // 如果没有任何新的资源需要加载，直接返回
    if (promisesToAwait.length === 0)
    {
        console.log('没有新的插件资源需要加载。');
        return;
    }

    // 第二轮：等待所有资源都下载完成
    // Promise.all 会并发执行所有加载任务，效率最高
    await Promise.all(promisesToAwait);

    // 第三轮：所有脚本都已执行，现在安全地注册 Vue 组件
    // 这一步是同步的，因为脚本已经在上一步执行完毕
    for (const asset of assets)
    {
        if (asset.componentType !== 'Vue') continue;

        const libraryName = asset.pluginId.replaceAll('.', '_');
        const pluginLib = (window as any)[libraryName];

        if (!pluginLib)
        {
            console.warn(`插件库 '${libraryName}' (来自 ${asset.pluginId}) 加载后未在 window 上找到。`);
            continue;
        }

        for (const componentName in pluginLib)
        {
            if (Object.prototype.hasOwnProperty.call(pluginLib, componentName))
            {
                // 检查是否已经注册过，避免重复工作
                if (!vueComponentRegistry.has(componentName))
                {
                    const component = pluginLib[componentName];
                    console.log(`注册 Vue 组件: ${componentName}`);
                    vueComponentRegistry.set(componentName, markRaw(component));
                }
            }
        }
    }
}

/**
 * 公开的入口函数，处理并发调用和状态锁定。
 * @param assets - 从API获取的动态资源列表。
 */
export async function loadAndRegisterPlugins(assets: DynamicAsset[]): Promise<void>
{
    // 如果已经有一个加载任务正在进行，直接返回该任务的 Promise
    if (loadingPromise)
    {
        return await loadingPromise;
    }

    // 创建一个新的加载任务 Promise，并用 loadingPromise 变量锁定它
    loadingPromise = (async () =>
    {
        try
        {
            await doLoadAndRegister(assets);
            console.log('所有新的动态插件已成功加载并注册', {
                vueComponents: Array.from(vueComponentRegistry.keys()),
                loadedScripts: Array.from(loadedScriptUrls),
                loadedStyles: Array.from(loadedStyleUrls)
            });
        } catch (error)
        {
            console.error('加载动态插件时发生错误:', error);
            // 将错误抛出，以便调用方可以捕获
            throw error;
        } finally
        {
            // 无论成功或失败，都必须清空锁，以便下次可以重新发起加载
            loadingPromise = null;
        }
    })();

    return await loadingPromise;
}

/**
 * 根据名称获取一个已注册的 Vue 插件组件。
 * @param name - 组件名（在后端RenderWithVueComponentAttribute中定义的ComponentName）。
 * @returns - Vue 组件定义或 undefined。
 */
export function getVuePluginComponent(name: string): Component | undefined
{
    return vueComponentRegistry.get(name);
}