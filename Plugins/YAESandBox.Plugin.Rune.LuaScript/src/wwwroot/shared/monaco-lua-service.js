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
 * 深度合并两个或多个对象。
 * 对于数组，它会连接它们并去重。对于对象，它会递归地合并它们的属性。
 * @param {...Object} objects - 要合并的对象。
 * @returns {Object} - 合并后的新对象。
 */
function deepMerge(...objects) {
    const isObject = obj => obj && typeof obj === 'object';

    return objects.reduce((prev, obj) => {
        Object.keys(obj).forEach(key => {
            const pVal = prev[key];
            const oVal = obj[key];

            if (Array.isArray(pVal) && Array.isArray(oVal)) {
                // 合并数组并基于 'name' 属性去重
                const combined = [...pVal, ...oVal];
                const unique = Array.from(new Map(combined.map(item => [item.name, item])).values());
                prev[key] = unique;
            } else if (isObject(pVal) && isObject(oVal)) {
                // 递归合并对象
                prev[key] = deepMerge(pVal, oVal);
            } else {
                // 否则直接赋值
                prev[key] = oVal;
            }
        });

        return prev;
    }, {});
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

/**
 * 核心功能：加载并合并所有 API 定义。
 * 它首先加载清单文件，然后根据清单并行加载所有独立的 API JSON 文件，
 * 最后将它们【深度合并】成一个单一的 API 数据对象。
 * @param {string} manifestUrl - 指向 api-manifest.json 的 URL。
 * @returns {Promise<Object>} - 一个解析为合并后 API 数据对象的 Promise。
 */
async function loadAndMergeApis(manifestUrl)
{
    console.log('[Lua Service] 开始加载 API 清单...');
    try
    {
        const manifestResponse = await fetch(manifestUrl);
        if (!manifestResponse.ok)
        {
            throw new Error(`无法加载清单文件: ${manifestResponse.statusText}`);
        }
        const manifest = await manifestResponse.json();

        // 创建一个包含所有 API 文件 fetch 操作的 Promise 数组
        const fetchPromises = manifest.apiFiles.map(filePath =>
        {
            // 使用 `new URL()` 来正确解析相对路径
            const apiUrl = new URL(filePath, manifestUrl).href;
            console.log(`[Lua Service] 发现 API 定义: ${apiUrl}`);
            return fetch(apiUrl).then(res =>
            {
                if (!res.ok) throw new Error(`加载 ${apiUrl} 失败`);
                return res.json();
            });
        });

        // 并行等待所有 API 文件加载完成
        const apis = await Promise.all(fetchPromises);

        const mergedApiData = deepMerge({}, ...apis);

        console.log('[Lua Service] 所有 API 定义已成功加载并合并!');
        return mergedApiData;

    } catch (error)
    {
        console.error('[Lua Service] 加载 API 数据时发生严重错误:', error);
        // 在出错时返回一个空对象，让编辑器仍然可以工作，只是没有智能提示。
        return {};
    }
}

// 将 configure 函数导出，并接收 manifestUrl
export async function configure(monaco, manifestUrl)
{
    // --- 1. 加载所有 API 定义 ---
    const apiData = await loadAndMergeApis(manifestUrl);

    // 如果 apiData 为空，说明加载失败，后续的提供者将无法工作。
    if (Object.keys(apiData).length === 0)
    {
        console.warn("[Lua Service] API 数据为空，智能提示和悬停功能将不可用。");
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
        triggerCharacters: ['.'], provideCompletionItems: (model, position) =>
        {
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

    // 为所有已存在的 lua 模型和未来创建的 lua 模型应用验证逻辑
    monaco.editor.getModels().forEach(model =>
    {
        if (model.getLanguageId() === 'lua')
        {
            // 首次加载时验证一次
            validate(model);
            // 监听内容变化，进行防抖验证
            model.onDidChangeContent(() => debouncedValidate(model));
        }
    });
    monaco.editor.onDidCreateModel(model =>
    {
        if (model.getLanguageId() === 'lua')
        {
            validate(model);
            model.onDidChangeContent(() => debouncedValidate(model));
        }
    });
}