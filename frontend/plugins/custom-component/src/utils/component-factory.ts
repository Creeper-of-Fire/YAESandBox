import type {Component} from 'vue';
import {defineAsyncComponent, h} from 'vue';
import {injectionKeys, injectionValues} from '../injection';

/**
 * 将编译后的 JavaScript 代码字符串转换为一个可执行的函数。
 * 这个函数会自动处理用户代码，将其最后一个表达式作为返回值。
 *
 * @param compiledJs - Sucrase 编译后的 JS 代码字符串。
 * @returns 一个工厂函数，调用后会返回 Vue 组件定义。
 * @throws 如果代码为空或无法解析，则抛出错误。
 */
function createExecutableFactory(compiledJs: string): Function
{
    // 1. 将编译后的代码按行分割
    const lines = compiledJs.trim().split('\n');

    // 2. 弹出最后一行，我们假设它是要返回的组件变量名
    const lastLine = lines.pop()?.trim();

    if (!lastLine)
    {
        throw new Error('Compiled code appears to be empty or invalid.');
    }

    // 3. 剩下的部分是组件的定义和辅助逻辑
    const definitionBody = lines.join('\n');

    // 4. 清理最后一行，确保它是一个合法的变量名（去掉可能的分号）
    const returnIdentifier = lastLine.endsWith(';') ? lastLine.slice(0, -1) : lastLine;

    // 5. 构造一个语法完全正确的函数体
    const functionBody = `
    ${definitionBody}
    return ${returnIdentifier};
  `;

    // 6. 创建并返回 Function 实例
    return new Function(...injectionKeys, functionBody);
}


/**
 * 从编译后的 JavaScript 代码字符串创建一个异步 Vue 组件。
 * 这是连接动态编译和 Vue 渲染系统的核心。
 *
 * @param compiledJs - Sucrase 编译后的 JS 代码字符串。
 * @param onError - 一个回调函数，用于在组件执行出错时报告错误。
 * @returns 一个 Vue 异步组件。
 */
export function createAsyncComponentFromJs(
    compiledJs: string,
    onError: (error: Error) => void
): Component
{
    try
    {
        const componentFactory = createExecutableFactory(compiledJs);

        return defineAsyncComponent(() =>
        {
            try
            {
                return Promise.resolve(componentFactory(...injectionValues));
            } catch (execError: any)
            {
                const error = new Error(`Component runtime error: ${execError.message}`);
                onError(error);
                // 返回一个渲染错误的组件
                return Promise.resolve({
                    render: () => h('div', {
                        style: 'color: red; border: 1px solid red; padding: 8px; font-family: monospace; white-space: pre-wrap;'
                    }, error.message)
                });
            }
        });

    } catch (factoryError: any)
    {
        // 这个错误发生在创建函数阶段（比如代码解析失败），比运行时错误更早
        onError(factoryError);

        // 返回一个静态的错误组件
        return {
            render: () => h('div', {
                style: 'color: red; border: 1px solid red; padding: 8px;'
            }, `Component factory creation failed: ${factoryError.message}`)
        };
    }
}