// monaco-lua-service.js
//
// 这是一个为 Monaco Editor 提供 Lua 语言高级功能的模块化服务。
// 它的核心职责包括：
// 1. 动态加载 API 定义：通过一个清单文件 (api-manifest.json) 发现并加载所有的 Lua API 定义 (例如标准库、自定义库)。
// 2. 提供智能补全 (Completion Provider): 提供关键字、全局函数、库和方法的补全。
// 3. 悬停提示 (Hover Provider): 为已知的 API 提供文档。
// 4. 代码检查 (Linter/Validation): 检查语法错误、未定义变量和作用域问题。
// 5. 代码格式化 (Document Formatting Provider): 集成 Prettier 和 Lua 插件，提供一键代码美化功能。
// 6. **兼容 Vite HMR (热模块替换)：确保在开发模式下重复加载时不会重复注册语言特性。**
//
// 此文件通过动态 import() 被 MonacoEditorWidget.vue 加载。

// 从 CDN 导入依赖库。这种方式无需本地打包，对现代浏览器非常友好。
import luaparse from 'https://esm.sh/luaparse@0.3.1';
import prettier from 'https://esm.sh/prettier@3.2.5/standalone';
import prettierPluginLua from 'https://esm.sh/@prettier/plugin-lua';
import * as registry from './api-registry.js';

/**
 * 防抖函数。
 * 防止函数在短时间内被高频触发，常用于输入验证、窗口大小调整等场景。
 * @param {Function} func - 需要防抖的函数。
 * @param {number} wait - 延迟执行的毫秒数。
 * @returns {Function} - 返回一个新的带防抖功能的函数。
 */
function debounce(func, wait)
{
    let timeout;
    return function executedFunction(...args)
    {
        const later = () =>
        {
            clearTimeout(timeout);
            func(...args);
        };
        clearTimeout(timeout);
        timeout = setTimeout(later, wait);
    };
}

/**
 * 将 API 定义中的 kind 字符串映射到 Monaco 的 CompletionItemKind 枚举值。
 * @param {string | undefined} kindString - 从 JSON 读取的 kind 字符串, 例如 "Variable"。
 * @param {import('monaco-editor')} monaco - Monaco 实例。
 * @returns {monaco.languages.CompletionItemKind} - Monaco 的枚举值。
 */
function mapKindStringToMonacoKind(kindString, monaco) {
    const mapping = {
        'Method': monaco.languages.CompletionItemKind.Method,
        'Function': monaco.languages.CompletionItemKind.Function,
        'Variable': monaco.languages.CompletionItemKind.Variable,
        'Property': monaco.languages.CompletionItemKind.Property,
        'Constant': monaco.languages.CompletionItemKind.Constant,
        'Module': monaco.languages.CompletionItemKind.Module,
        'Keyword': monaco.languages.CompletionItemKind.Keyword,
    };

    // 如果找不到匹配项，提供一个安全的回退值
    return mapping[kindString] || monaco.languages.CompletionItemKind.Function;
}

// =======================================================================================
// 全局配置标志位
// IMPORTANT / 重要事项:
//
// This is a globally unique ID to prevent the configuration from running more than once.
// If you are copying this code for your own plugin, PLEASE generate a NEW UUID.
// Do not reuse this one, to avoid conflicts with other plugins.
//
// 这是一个全局唯一的ID，用于防止配置逻辑重复运行。
// 如果您正在为自己的插件复制此代码，请务必生成一个新的UUID。
// 不要重复使用此ID，以避免与其他插件发生冲突。
//
const CONFIGURED_FLAG_UUID = '__LUA_SERVICE_CONFIGURED_FLAG_06bb2299-8b25-4f19-901c-f678223ff2bd';
// =======================================================================================

// 将 configureGlobalLuaProviders 函数导出
export async function configureGlobalLuaProviders(monaco)
{
    if (monaco[CONFIGURED_FLAG_UUID]) {
        return; // 确保只配置一次
    }

    /**
     * Lua 语言的关键字列表。
     * 每个关键字都配置了智能代码片段 (Snippet)，以提高编码效率。
     * @type {Array<import('monaco-editor').languages.CompletionItem>}
     */
    const luaKeywords = [
        {label: 'and', kind: monaco.languages.CompletionItemKind.Keyword, insertText: 'and '},
        {label: 'break', kind: monaco.languages.CompletionItemKind.Keyword, insertText: 'break'},
        {label: 'do', kind: monaco.languages.CompletionItemKind.Keyword, insertText: 'do'},
        {label: 'else', kind: monaco.languages.CompletionItemKind.Keyword, insertText: 'else'},
        {label: 'elseif', kind: monaco.languages.CompletionItemKind.Keyword, insertText: 'elseif '},
        {label: 'end', kind: monaco.languages.CompletionItemKind.Keyword, insertText: 'end'},
        {label: 'false', kind: monaco.languages.CompletionItemKind.Keyword, insertText: 'false'},
        {
            label: 'for',
            kind: monaco.languages.CompletionItemKind.Keyword,
            insertText: 'for ${1:i} = ${2:1}, ${3:10} do\n\t$0\nend',
            documentation: '创建一个数字型 for 循环'
        },
        {
            label: 'function',
            kind: monaco.languages.CompletionItemKind.Keyword,
            insertText: 'function ${1:name}(${2:args})\n\t$0\nend',
            documentation: '创建一个函数定义'
        },
        {
            label: 'if',
            kind: monaco.languages.CompletionItemKind.Keyword,
            insertText: 'if ${1:condition} then\n\t$0\nend',
            documentation: '创建一个 if 条件语句'
        },
        {label: 'in', kind: monaco.languages.CompletionItemKind.Keyword, insertText: 'in '},
        {label: 'local', kind: monaco.languages.CompletionItemKind.Keyword, insertText: 'local '},
        {label: 'nil', kind: monaco.languages.CompletionItemKind.Keyword, insertText: 'nil'},
        {label: 'not', kind: monaco.languages.CompletionItemKind.Keyword, insertText: 'not '},
        {label: 'or', kind: monaco.languages.CompletionItemKind.Keyword, insertText: 'or '},
        {
            label: 'repeat',
            kind: monaco.languages.CompletionItemKind.Keyword,
            insertText: 'repeat\n\t$0\nuntil ${1:condition}',
            documentation: '创建一个 repeat...until 循环'
        },
        {label: 'return', kind: monaco.languages.CompletionItemKind.Keyword, insertText: 'return '},
        {label: 'then', kind: monaco.languages.CompletionItemKind.Keyword, insertText: 'then'},
        {label: 'true', kind: monaco.languages.CompletionItemKind.Keyword, insertText: 'true'},
        {label: 'until', kind: monaco.languages.CompletionItemKind.Keyword, insertText: 'until '},
        {
            label: 'while',
            kind: monaco.languages.CompletionItemKind.Keyword,
            insertText: 'while ${1:condition} do\n\t$0\nend',
            documentation: '创建一个 while 循环'
        },
    ];

    // --- 2. 注册动态自动补全提供者 ---
    monaco.languages.registerCompletionItemProvider('lua', {
        triggerCharacters: ['.'], 
        provideCompletionItems: (model, position) =>
        {
            const contextId = model.__my_context_id; // 直接获取 contextId
            if (!contextId) {
                return { suggestions: [] }; // 如果上下文未设置，不提供任何补全
            }
            const apiData = registry.get(contextId); // 从注册中心获取正确的 API 数据
            
            const lineContent = model.getLineContent(position.lineNumber);
            const textBeforeCursor = lineContent.substring(0, position.column - 1);

            // 正则匹配光标前可能存在的调用链表达式
            const expressionMatch = textBeforeCursor.match(/[\w\.]*$/);
            if (!expressionMatch) return {suggestions: []};

            const fullExpression = expressionMatch[0];
            const parts = fullExpression.split('.');

            // 场景一：方法补全上下文 (表达式包含点)
            if (fullExpression.includes('.'))
            {
                if (parts.length < 2 && !fullExpression.endsWith('.')) return {suggestions: []};

                const objectName = parts[parts.length - 2];

                const apiObject = apiData[objectName];
                if (!apiObject || !apiObject.methods) return {suggestions: []};

                const word = model.getWordUntilPosition(position);
                const replacementRange = {
                    startLineNumber: position.lineNumber,
                    startColumn: word.startColumn,
                    endLineNumber: position.lineNumber,
                    endColumn: word.endColumn,
                };

                const suggestions = apiObject.methods.map(method => ({
                    label: method.name,
                    kind: mapKindStringToMonacoKind(method.kind, monaco),
                    documentation: {value: method.documentation, isTrusted: true},
                    insertText: method.insertText ?? method.name,
                    insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet,
                    range: replacementRange,
                }));
                return {suggestions};
            }

            // 场景二：全局补全上下文
            const word = model.getWordUntilPosition(position);
            const range = {
                startLineNumber: position.lineNumber,
                endLineNumber: position.lineNumber,
                startColumn: word.startColumn,
                endColumn: word.endColumn,
            };

            const keywordSuggestions = luaKeywords.map(k => ({
                ...k, range, insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet
            }));

            let globalSuggestions = [];
            if (apiData.global && apiData.global.methods)
            {
                globalSuggestions = apiData.global.methods.map(method => ({
                    label: method.name,
                    kind: mapKindStringToMonacoKind(method.kind, monaco),
                    documentation: {value: method.documentation, isTrusted: true},
                    insertText: method.insertText,
                    insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet,
                    range: range,
                }));
            }

            const librarySuggestions = Object.keys(apiData).filter(name => name !== 'global').map(libName => ({
                label: libName,
                kind: monaco.languages.CompletionItemKind.Module,
                documentation: apiData[libName].documentation || `Lua 库: ${libName}`,
                insertText: libName,
                range: range,
            }));

            return {suggestions: [...keywordSuggestions, ...globalSuggestions, ...librarySuggestions]};
        }
    });


    // --- 3. 注册动态悬停提示提供者 ---
    monaco.languages.registerHoverProvider('lua', {
        provideHover: (model, position) =>
        {
            const contextId = model.__my_context_id; // 直接获取 contextId
            if (!contextId) {
                return null;
            }
            const apiData = registry.get(contextId); // 从注册中心获取正确的 API 数据
            
            const wordInfo = model.getWordAtPosition(position);
            if (!wordInfo)
            {
                return null;
            }

            const lineContent = model.getLineContent(position.lineNumber);

            // --- 步骤 1: 识别光标所在的完整表达式及其在行中的起始位置 ---
            let fullExpression = '';
            let exprStartIndex = -1; // 使用 0-based 索引进行计算
            const expressionRegex = /[\w\.]+/g;
            let match;

            while ((match = expressionRegex.exec(lineContent)) !== null)
            {
                const startIndex = match.index;
                const endIndex = startIndex + match[0].length;
                // Monaco 的列是 1-based, JS 的 index 是 0-based
                if (position.column >= startIndex + 1 && position.column <= endIndex + 1)
                {
                    fullExpression = match[0];
                    exprStartIndex = startIndex; // 记下表达式的 0-based 起始索引
                    break;
                }
            }

            if (!fullExpression || fullExpression.endsWith('.'))
            {
                return null;
            }

            const parts = fullExpression.split('.');
            const root = parts[0];

            // --- 步骤 2: 验证表达式的根 ---
            if (!apiData[root])
            {
                // 处理全局函数 (例如 'print')
                const globalFunc = apiData.global?.methods.find(m => m.name === root);
                if (globalFunc && parts.length === 1)
                {
                    return {
                        range: new monaco.Range(position.lineNumber, exprStartIndex + 1, position.lineNumber, exprStartIndex + 1 + root.length),
                        contents: [{value: `\`\`\`lua\n${globalFunc.signature}\n\`\`\``}, {value: globalFunc.documentation}]
                    };
                }
                return null; // 未知的根，不提供任何提示
            }

            // --- 步骤 3: 基于位置精确判断悬停目标 ---

            // 场景 A: 悬停在根对象上 (例如 a.b -> 'a', debug.debug -> 第一个 'debug')
            // 判断条件：当前单词的起始列(1-based) === 表达式的起始列(0-based) + 1
            if (wordInfo.startColumn === exprStartIndex + 1)
            {
                return {
                    range: new monaco.Range(position.lineNumber, wordInfo.startColumn, position.lineNumber, wordInfo.endColumn),
                    contents: [{value: apiData[root].documentation || `Lua 库: ${root}`}]
                };
            }

            // 场景 B: 悬停在方法上 (例如 a.b -> 'b', debug.debug -> 第二个 'debug')
            // 我们目前只支持简单的 object.method 形式
            if (parts.length === 2)
            {
                // 计算出方法的预期起始列
                const methodExpectedStartColumn = exprStartIndex + parts[0].length + 1 + 1; // exprStart + rootLength + dotLength + 1-based

                // 判断条件：当前单词的起始列 === 我们计算出的方法预期起始列
                if (wordInfo.startColumn === methodExpectedStartColumn)
                {
                    const objectName = parts[0];
                    const methodName = parts[1];
                    const method = apiData[objectName]?.methods.find(m => m.name === methodName);
                    if (method)
                    {
                        return {
                            range: new monaco.Range(position.lineNumber, wordInfo.startColumn, position.lineNumber, wordInfo.endColumn),
                            contents: [{value: `\`\`\`lua\n${method.signature}\n\`\`\``}, {value: method.documentation}]
                        };
                    }
                }
            }

            // 对于无法识别的调用链部分 (例如悬停在 'day' in 'os.date.day'，因为我们没有 'os.date' 的子级定义)，返回 null。
            return null;
        }
    });


    // --- 4. 注册代码格式化提供者 ---
    monaco.languages.registerDocumentFormattingEditProvider('lua', {
        async provideDocumentFormattingEdits(model)
        {
            const unformattedText = model.getValue();
            try
            {
                // 使用 Prettier 进行格式化
                const formattedText = await prettier.format(unformattedText, {
                    parser: "lua", plugins: [prettierPluginLua], // 在此可以添加更多 Prettier 的配置项
                    tabWidth: 2, singleQuote: false,
                });

                return [
                    {
                        range: model.getFullModelRange(), // 替换整个文档
                        text: formattedText,
                    }
                ];
            } catch (err)
            {
                console.error("代码格式化失败:", err);
                // 在格式化失败时通知用户（例如通过日志），并返回空数组
                return [];
            }
        }
    });

    // --- 5. 设置实时语法验证 ---
    const owner = 'lua-linter';

    const validate = (model) =>
    {
        const code = model.getValue();
        const markers = [];
        
        const contextId = model.__my_context_id; // 直接获取 contextId
        if (!contextId) {
            // 如果上下文 ID 尚未设置，则清除此模型的所有标记并中止。
            // 这是解决竞态条件的关键。
            monaco.editor.setModelMarkers(model, owner, []);
            return;
        }
        const apiData = registry.get(contextId); // 从注册中心获取正确的 API 数据
        if (!apiData || Object.keys(apiData).length === 0) {
            // 如果找到了 contextId 但注册表中没有数据 (可能正在加载)，也暂时不报错
            monaco.editor.setModelMarkers(model, owner, markers);
            return;
        }

        try
        {
            // 1. 语法解析
            const ast = luaparse.parse(code, {locations: true});

            // 2. 静态分析
            class ScopeManager
            {
                constructor(predefined)
                {
                    this.scopes = []; // 作用域堆栈
                    this.predefinedGlobals = new Set(predefined);
                    this.enter(); // 进入全局作用域
                }

                enter()
                {
                    this.scopes.push(new Set());
                }

                exit()
                {
                    this.scopes.pop();
                }

                define(name)
                {
                    if (this.scopes.length > 0)
                    {
                        this.scopes[this.scopes.length - 1].add(name);
                    }
                }

                // 定义一个全局变量
                defineGlobal(name)
                {
                    this.scopes[0].add(name);
                }

                isDefined(name)
                {
                    for (let i = this.scopes.length - 1; i >= 0; i--)
                    {
                        if (this.scopes[i].has(name)) return true;
                    }
                    return this.predefinedGlobals.has(name);
                }
            }

            const predefined = new Set(Object.keys(apiData));
            if (apiData.global && apiData.global.methods)
            {
                apiData.global.methods.forEach(m => predefined.add(m.name));
            }

            const scopeManager = new ScopeManager(predefined);

            const traverse = (node) =>
            {
                if (!node) return;

                // 使用 Visitor 模式
                const visitor = visitors[node.type];
                if (visitor)
                {
                    visitor(node);
                } else
                {
                    // 默认遍历所有对象和数组类型的子节点
                    for (const key in node)
                    {
                        if (key === 'loc') continue; // 跳过位置信息
                        const child = node[key];
                        if (typeof child === 'object' && child !== null)
                        {
                            if (Array.isArray(child))
                            {
                                child.forEach(traverse);
                            } else
                            {
                                traverse(child);
                            }
                        }
                    }
                }
            };

            const visitors = {
                TableConstructorExpression: (node) =>
                {
                    node.fields.forEach(field =>
                    {
                        // field 的类型决定了如何处理
                        // 1. 对于 { key = value } (TableKeyString), 'key' 是标识符但不是变量，我们只检查 'value'
                        // 2. 对于 { [expr1] = expr2 } (TableKey), 'expr1' 和 'expr2' 都是表达式，都需要检查
                        // 3. 对于 { value } (TableValue), 'value' 是表达式，需要检查
                        if (field.type === 'TableKeyString')
                        {
                            // 这是您遇到的情况，比如 { role = "system" }
                            // 'role' 是 field.key，但我们不应该检查它。
                            // 我们只需要检查它的值部分。
                            traverse(field.value);
                        } else if (field.type === 'TableKey')
                        {
                            // 例如 t = { [myVar] = 10 }，myVar 需要被检查
                            traverse(field.key);
                            traverse(field.value);
                        } else if (field.type === 'TableValue')
                        {
                            // 例如 t = { myVar }, myVar 需要被检查
                            traverse(field.value);
                        }
                    });
                }, Chunk: (node) =>
                {
                    scopeManager.enter();
                    node.body.forEach(traverse);
                    scopeManager.exit();
                }, LocalStatement: (node) =>
                {
                    node.init.forEach(traverse);
                    // For `local a, b = 1` luaparse creates more `init` than `variables`
                    node.variables.forEach(v => scopeManager.define(v.name));
                }, AssignmentStatement: (node) =>
                {
                    node.init.forEach(traverse);
                    node.variables.forEach(v =>
                    {
                        if (v.type === 'Identifier' && !scopeManager.isDefined(v.name))
                        {
                            // 如果变量未在任何作用域定义，则视为定义了一个新的全局变量
                            scopeManager.defineGlobal(v.name);
                        }
                        traverse(v); // 递归遍历，以处理 table.key = value 等情况
                    });
                }, FunctionDeclaration: (node) =>
                {
                    if (node.identifier)
                    {
                        if (node.isLocal)
                        {
                            scopeManager.define(node.identifier.name);
                        } else
                        {
                            // 全局函数
                            scopeManager.defineGlobal(node.identifier.name);
                        }
                    }
                    scopeManager.enter();
                    node.parameters.forEach(p => scopeManager.define(p.name));
                    node.body.forEach(traverse);
                    scopeManager.exit();
                }, ForNumericStatement: (node) =>
                {
                    traverse(node.start);
                    traverse(node.end);
                    if (node.step) traverse(node.step);
                    scopeManager.enter();
                    scopeManager.define(node.variable.name);
                    traverse(node.body);
                    scopeManager.exit();
                }, ForGenericStatement: (node) =>
                {
                    node.iterators.forEach(traverse);
                    scopeManager.enter();
                    node.variables.forEach(v => scopeManager.define(v.name));
                    traverse(node.body);
                    scopeManager.exit();
                }, IfStatement: (node) =>
                {
                    node.clauses.forEach(traverse);
                }, IfClause: (node) =>
                {
                    traverse(node.condition);
                    traverse(node.body);
                }, ElseifClause: (node) =>
                {
                    traverse(node.condition);
                    traverse(node.body);
                }, ElseClause: (node) =>
                {
                    traverse(node.body);
                }, Identifier: (node) =>
                {
                    if (!scopeManager.isDefined(node.name))
                    {
                        markers.push({
                            message: `未定义变量或函数: '${node.name}'`,
                            severity: monaco.MarkerSeverity.Error,
                            startLineNumber: node.loc.start.line,
                            startColumn: node.loc.start.column + 1,
                            endLineNumber: node.loc.end.line,
                            endColumn: node.loc.end.column + 1
                        });
                    }
                }, // 需要跳过特定子节点遍历的节点
                MemberExpression: (node) =>
                {
                    traverse(node.base); // 只检查对象，不检查属性名
                    // 如果是 t["key"] 形式，key 也是一个表达式，需要遍历
                    if (node.indexer === '[')
                    {
                        traverse(node.identifier);
                    }
                },
            };

            // 开始遍历
            traverse(ast);

        } catch (e)
        {
            // 捕获纯语法错误
            if (e.line)
            {
                markers.push({
                    message: `语法错误: ${e.message}`,
                    severity: monaco.MarkerSeverity.Error,
                    startLineNumber: e.line,
                    startColumn: e.column,
                    endLineNumber: e.line,
                    endColumn: e.column + 1,
                });
            }
        } finally
        {
            monaco.editor.setModelMarkers(model, owner, markers);
        }
    };
    const debouncedValidate = debounce(validate, 200); // 创建防抖版的验证函数

    /**
     * 为指定的 Monaco 模型设置智能验证逻辑。
     * 它通过 Object.defineProperty 拦截对 `__my_context_id` 的赋值操作，
     * 当该值被设置时，立即触发一次验证。
     * @param {import('monaco-editor').editor.ITextModel} model
     */
    const setupModelIntelligence = (model) => {
        // 防止对同一个 model 重复定义
        const descriptor = Object.getOwnPropertyDescriptor(model, '__my_context_id');
        if (descriptor && !descriptor.writable) { // 如果属性已存在且不可写, 通常意味着我们的 getter/setter 已设置
            return;
        }

        let _contextId = model.__my_context_id; // 捕获可能已存在的初始值

        // 使用 getter/setter 重新定义 __my_context_id
        Object.defineProperty(model, '__my_context_id', {
            configurable: true,
            enumerable: true,
            get() {
                return _contextId;
            },
            set(newValue) {
                const oldValue = _contextId;
                _contextId = newValue;
                // 当上下文 ID 被有效设置或更改时，立即触发一次验证
                if (newValue && newValue !== oldValue) {
                    validate(model); // 使用非防抖版本以获得即时反馈
                }
            }
        });

        // 绑定标准的 onDidChangeContent 事件，用于用户输入时的验证
        model.onDidChangeContent(() => debouncedValidate(model));

        // 首次设置时，也运行一次验证（如果 contextId 已有值）
        validate(model);
    };

    // 为所有已存在和未来创建的 lua 模型应用此智能设置
    monaco.editor.getModels().forEach(model => {
        if (model.getLanguageId() === 'lua') {
            setupModelIntelligence(model);
        }
    });
    monaco.editor.onDidCreateModel(model => {
        if (model.getLanguageId() === 'lua') {
            setupModelIntelligence(model);
        }
    });

    monaco[CONFIGURED_FLAG_UUID] = true;
}