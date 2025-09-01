// useScriptCompiler.ts
import {ref, type Ref, watch} from 'vue';
import {parse} from 'acorn';

// 定义编译器选项接口
export interface ScriptCompilerOptions
{
    scriptRef: Ref<string>;
    expectedFunctionName: string;
}

/**
 * 一个可复用的 Vue Composable，用于编译和验证用户脚本。
 * 它使用 Acorn 进行精确的语法错误定位。
 *
 * @param options - 包含脚本引用和期望函数名的选项对象
 */
export function useScriptCompiler(options: ScriptCompilerOptions)
{
    const {scriptRef, expectedFunctionName} = options;

    const error = ref<string | null>(null);
    const compiledFunction = ref<((content: string) => string) | null>(null);

    const validateAndCompile = (script: string) =>
    {
        // 重置状态
        error.value = null;
        compiledFunction.value = null;

        if (!script.trim())
        {
            return; // 空脚本是有效的，什么也不做
        }

        // 阶段 1: 使用 Acorn 进行语法分析
        try
        {
            parse(script, {ecmaVersion: 'latest', sourceType: 'module'});
        } catch (e)
        {
            if (e instanceof SyntaxError && 'loc' in e)
            {
                const loc = (e as any).loc;
                error.value = `语法错误: ${e.message}\n(在第 ${loc.line} 行, 第 ${loc.column + 1} 列)`;
            }
            else
            {
                error.value = (e as Error).message;
            }
            return; // 语法错误，终止执行
        }

        // 阶段 2: 运行时验证和编译
        try
        {
            const executionWrapper = `${script}\n;return ${expectedFunctionName};`;
            const func = new Function(executionWrapper)();

            if (typeof func !== 'function')
            {
                error.value = `逻辑错误: 未找到名为 "${expectedFunctionName}" 的函数。请确保您已正确定义该函数。`;
                return;
            }

            // 编译成功
            compiledFunction.value = func;

        } catch (e)
        {
            // 捕获运行时错误，例如引用了未定义的变量
            error.value = `运行时错误: ${(e as Error).message}`;
        }
    };

    // 监听外部传入的 scriptRef 变化，自动触发验证
    watch(scriptRef, (newScript) =>
    {
        validateAndCompile(newScript);
    }, {immediate: true});

    return {
        error,
        compiledFunction,
        // 也可暴露一个手动调用的方法，虽然 watch 已经处理了大部分情况
        validateAndCompile,
    };
}