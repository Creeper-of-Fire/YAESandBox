// 这个文件将通过动态 import() 被前端加载
// 它导出一个对象，该对象包含 Monaco Editor 的语言服务配置

// !!! 新增：从 CDN 导入 luaparse !!!
// 我们使用 unpkg，因为它能很好地处理 UMD 模块
import luaparse from 'https://esm.sh/luaparse';

// 自定义的补全项 (这部分不变)
const ctxCompletions = [
    {
        label: 'ctx.get',
        kind: monaco.languages.CompletionItemKind.Function,
        documentation: "从上下文获取指定名称的变量值。",
        insertText: "get('${1:variableName}')", // 使用 snippet 语法
        insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet
    },
    {
        label: 'ctx.set',
        kind: monaco.languages.CompletionItemKind.Function,
        documentation: "向上下文设置指定名称的变量值。",
        insertText: "set('${1:variableName}', ${2:value})",
        insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet
    }
];

// !!! 新增：防抖函数，避免在快速输入时频繁验证 !!!
function debounce(func, wait) {
    let timeout;
    return function executedFunction(...args) {
        const later = () => {
            clearTimeout(timeout);
            func(...args);
        };
        clearTimeout(timeout);
        timeout = setTimeout(later, wait);
    };
}


// 导出语言服务配置
export default {
    /**
     * Monaco Editor 的语言服务配置
     */
    configure: (monaco) => {
        // --- 自动补全部分 (不变) ---
        monaco.languages.registerCompletionItemProvider('lua', {
            provideCompletionItems: (model, position) => {
                const textUntilPosition = model.getValueInRange({
                    startLineNumber: position.lineNumber,
                    startColumn: 1,
                    endLineNumber: position.lineNumber,
                    endColumn: position.column
                });

                if (/ctx\.$/.test(textUntilPosition)) {
                    return {suggestions: ctxCompletions};
                }
                return {suggestions: []};
            },
            triggerCharacters: ['.']
        });

        // 注册一个悬停提示提供者 (如果需要)
        monaco.languages.registerHoverProvider('lua', {
            provideHover: (model, position) => {
                const word = model.getWordAtPosition(position);
                if (word && word.word === 'get') { // 这里可以做更复杂的判断，例如检查它是否是 ctx.get
                    return {
                        range: new monaco.Range(position.lineNumber, word.startColumn, position.lineNumber, word.endColumn),
                        contents: [
                            { value: '**ctx.get(variableName: string): any**' },
                            { value: '从上下文获取指定名称的变量值。' }
                        ]
                    };
                }
                if (word && word.word === 'set') { // 这里可以做更复杂的判断，例如检查它是否是 ctx.set
                    return {
                        range: new monaco.Range(position.lineNumber, word.startColumn, position.lineNumber, word.endColumn),
                        contents: [
                            { value: '**ctx.set(variableName: string , value): any**' },
                            { value: '从上下文获取指定名称的变量值。' }
                        ]
                    };
                }
                return null;
            }
        });

        // --- !!! 新增：实时语法验证部分 !!! ---
        const owner = 'lua-linter'; // 用于标记这些错误是我们添加的
        const model = monaco.editor.getModels()[0]; // 获取当前编辑器的模型

        const validate = () => {
            const code = model.getValue();
            const markers = [];
            try {
                // 尝试解析代码
                const parser = luaparse.default || luaparse;
                parser.parse(code, {
                    wait: false,
                    scope: {
                        ctx: true
                    }
                });
                // 如果没有错误，清空之前的标记
                monaco.editor.setModelMarkers(model, owner, []);
            } catch (e) {
                // 如果解析失败，捕获错误
                if (e.line) {
                    markers.push({
                        message: e.message,
                        severity: monaco.MarkerSeverity.Error,
                        startLineNumber: e.line,
                        startColumn: e.column,
                        endLineNumber: e.line,
                        endColumn: e.column + 1 // 至少标记一个字符
                    });
                }
                // 将错误标记应用到编辑器
                monaco.editor.setModelMarkers(model, owner, markers);
            }
        };

        // 创建一个防抖版本的验证函数
        const debouncedValidate = debounce(validate, 500); // 延迟 500ms

        // 监听内容变化，并调用防抖验证
        model.onDidChangeContent(() => {
            debouncedValidate();
        });

        // 初始加载时也验证一次
        validate();
    }
};