import { transform } from 'sucrase';

/**
 * 使用 Sucrase 将 JSX 字符串编译成 Vue 3 兼容的 JavaScript 字符串。
 *
 * @param jsxCode 包含 JSX 的原始源代码字符串。
 * @returns 编译后的 JavaScript 代码字符串。
 * @throws 如果 JSX 语法无效，会抛出一个带有详细信息的错误。
 */
export function compileJsx(jsxCode: string): string {
    try {
        const { code } = transform(jsxCode, {
            transforms: ['typescript','jsx'],

            // 【关键】告诉 Sucrase 将 JSX 元素 (e.g., <div />) 转换成什么函数调用。
            // 对于 Vue 3，我们希望它变成 Vue.h('div')。
            jsxPragma: 'Vue.h',

            // 【关键】告诉 Sucrase 如何处理 JSX 片段 (e.g., <>...</>)。
            // 对于 Vue 3，我们希望它变成 Vue.Fragment。
            jsxFragmentPragma: 'Vue.Fragment',

            // 可选：在生产模式下可以开启以获得微小的性能提升
            production: true,
        });

        if (!code) {
            throw new Error('Compilation resulted in empty code. Check the input source.');
        }

        return code;

    } catch (error: any) {
        // 捕获 Sucrase 抛出的语法错误，并重新包装成一个更易于上层处理的错误。
        // error.message 通常会包含行列号等有用信息。
        console.error('JSX compilation error details:', error);
        throw new Error(`JSX Compilation Failed: ${error.message}`);
    }
}