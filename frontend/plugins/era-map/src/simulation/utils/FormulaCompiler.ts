export class FormulaCompiler {
    /**
     * 将一个包含变量 'r' 的数学表达式字符串编译成一个高效的 JS 函数。
     * @param formulaString - 例如 '1000 / (r * r)'
     * @returns 一个函数 (r: number) => number
     */
    public static compile(formulaString: string): (r: number) => number {
        try {
            // 使用 Function 构造函数是一种受控的、高性能的动态代码执行方式。
            // 它在全局作用域中创建函数，比 eval() 更安全、更高效。
            // 我们只暴露 'r' 这个参数给它。
            return new Function('r', `return ${formulaString};`) as (r: number) => number;
        } catch (error) {
            console.error(`Failed to compile formula: "${formulaString}"`, error);
            // 返回一个无操作的函数作为降级方案
            return () => 0;
        }
    }
}