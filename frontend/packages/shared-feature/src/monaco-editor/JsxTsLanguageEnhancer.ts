import { type MonacoEditor } from '@guolao/vue-monaco-editor';
import { parse } from '@babel/parser';
//@ts-ignore
import traverse from '@babel/traverse';
//@ts-ignore
import MonacoJSXHighlighter from 'monaco-jsx-highlighter';
import type { MonacoLanguageEnhancer, ExtraLib } from './types.ts';

type MonacoDisposable = { dispose: () => void };

export class JsxTsLanguageEnhancer implements MonacoLanguageEnhancer {
    private disposables: MonacoDisposable[] = [];
    private monacoJSXHighlighter: any = null;
    private isConfigured = false;
    private readonly language: string;

    constructor(language: string) {
        this.language = language;
    }

    public setup(editor: any, monaco: MonacoEditor, extraLibs: ExtraLib[]): void {
        this.configureMonacoEnvironment(monaco);
        this.setupJsxHighlighter(editor, monaco);
        this.applyMonkeyPatches(monaco);
        this.setupExtraLibs(monaco, extraLibs);

        // 激活高亮和注释命令
        this.monacoJSXHighlighter.highlightOnDidChangeModelContent(100);
        const commentCommandDisposable = this.monacoJSXHighlighter.addJSXCommentCommand();

        // 收集所有需要清理的资源
        this.disposables.push({ dispose: commentCommandDisposable });
    }

    public dispose(): void {
        console.log('[JsxTsLanguageEnhancer] Disposing all enhancements...');
        this.disposables.forEach(d => d.dispose());
        this.disposables = [];
    }

    private configureMonacoEnvironment(monaco: MonacoEditor): void {
        if (this.isConfigured) return;

        console.log('[SmartEditor] Performing configuration of Monaco environment for JSX/TSX...');

        // --- 为 TypeScript 配置 ---
        monaco.languages.typescript.typescriptDefaults.setCompilerOptions({
            // 告诉 TypeScript 编译器如何处理 JSX
            // 'Preserve' 表示保留 JSX 语法，让 Babel 等下游工具处理，这在我们的场景中最合适。
            jsx: monaco.languages.typescript.JsxEmit.Preserve,

            // 其他推荐选项，增强体验
            target: monaco.languages.typescript.ScriptTarget.ESNext,
            module: monaco.languages.typescript.ModuleKind.ESNext,
            moduleResolution: monaco.languages.typescript.ModuleResolutionKind.NodeJs,
            allowNonTsExtensions: true,
            allowJs: true,
            esModuleInterop: true,
        });

        // --- 为 JavaScript 配置 ---
        // monaco.languages.typescript.javascriptDefaults.setCompilerOptions({
        //   // 告诉 JavaScript 服务允许 JSX 语法
        //   jsx: monaco.languages.typescript.JsxEmit.None,
        //
        //   // 其他推荐选项
        //   target: monaco.languages.typescript.ScriptTarget.ESNext,
        //   allowNonTsExtensions: true,
        //   checkJs: true, // 在 JS 文件中也进行一些基本的类型检查
        // });

        this.isConfigured = true;
    }

    private setupJsxHighlighter(editor: any, monaco: MonacoEditor): void {
        // 1. 创建 Babel 解析函数
        const babelParse = (code: any, options = {}) =>
        {
            return parse(
                code,
                {
                    sourceType: 'module',
                    errorRecovery: true,
                    plugins: [
                        // 1. 基础语法支持
                        'jsx',        // 必须，用于解析 JSX
                        ...(this.language === 'typescript' ? ['typescript' as const] : []),  // 必须，用于解析 TypeScript 类型、泛型等


                        // 2. 现代 Class 特性 (非常重要)
                        'classProperties',      // 支持 `class MyClass { myProp = 1; }`
                        'classPrivateProperties', // 支持 `class MyClass { #privateField = 2; }`
                        'classPrivateMethods',    // 支持 `class MyClass { #privateMethod() {} }`
                        'classStaticBlock',       // 支持 `class A { static { ... } }`

                        // 3. 装饰器 (Decorators) - 框架和库中极其常用
                        // 注意: 'decorators-legacy' 是用于处理当前广泛使用的旧版装饰器提案。
                        // 你也可以用 ['decorators', { decoratorsBeforeExport: true }] 来支持新提案，但前者兼容性更好。
                        'decorators-legacy',

                        // 4. 模块和动态导入
                        'dynamicImport',    // 支持 `import()`
                        'topLevelAwait',    // 支持在模块顶层使用 await

                        // 5. 处于提案阶段的常用语法 (增加健壮性)
                        'doExpressions',          // 支持 `const x = do { ... }`
                        // 'pipelineOperator',       // 支持 `value |> func` (需要指定提案版本)
                        // ['pipelineOperator', { proposal: 'minimal' }],
                        // 'optionalChainingAssign', // 支持 `a?.b ??= c`
                        'importAssertions',       // 支持 `import json from './foo.json' assert { type: 'json' }`

                        // 6. 其他有用的语法
                        'decimal',                // 支持 `123.45m` 语法
                        'exportDefaultFrom',      // 支持 `export v from 'mod'`
                        'throwExpressions',       // 支持 `() => throw new Error('...')`

                    ],
                    ...options
                })
        }

        // 2. 实例化高亮器
        this.monacoJSXHighlighter = new MonacoJSXHighlighter(
            monaco,
            babelParse,
            traverse,
            editor
        );
    }

    private applyMonkeyPatches(monaco:MonacoEditor): void {
        if (!this.monacoJSXHighlighter) return;

        const monacoJSXHighlighter = this.monacoJSXHighlighter;

        console.log('[JsxTsLanguageEnhancer] Applying monkey patches...');

        // 安全补丁
        // 1. 保存原始的 extractAllDecorators 方法
        const originalExtractAllDecorators = monacoJSXHighlighter.extractAllDecorators.bind(monacoJSXHighlighter);

        // 2. 用我们自己的安全版本覆盖它
        monacoJSXHighlighter.extractAllDecorators = (jsxManager: any) =>
        {
            // **这就是我们的安全检查！**
            // 如果编辑器实例或其模型已经不存在了，就直接返回一个空数组，
            // 什么也不做，从而避免崩溃。
            if (!monacoJSXHighlighter.monacoEditor?.getModel())
            {
                // console.warn('MonacoJSXHighlighter: Editor disposed, skipping decoration.');
                return [];
            }

            // 如果编辑器还存在，就调用原始的方法
            return originalExtractAllDecorators(jsxManager);
        };

        console.log('MonacoJSXHighlighter has been monkey-patched for safety.');

        // 2. 评论命令 Bug 修复补丁
        monacoJSXHighlighter.addJSXCommentCommand = function (
            getAstPromise: any, // 使用 function 关键字以保留正确的 `this` 上下文
            onParseErrors = (error: any) => error,
            editorInstance = monacoJSXHighlighter.monacoEditor
        )
        {
            // 内部几乎所有的代码都和原始代码一样
            getAstPromise = getAstPromise ?? this.getAstPromise;
            const COMMENT_ACTION_ID = 'editor.action.commentLine'; // 确保常量被定义

            if (this._editorCommandId)
            {
                this._isJSXCommentCommandActive = true;
                return this.editorCommandOnDispose;
            }

            // 👇 --- 这是唯一的修改点 --- 👇
            this._editorCommandId = editorInstance.addCommand(
                monaco.KeyMod.CtrlCmd | monaco.KeyCode.Slash, // 使用正确的 KeyCode.Slash
                () =>
                {
                    if (!this._isJSXCommentCommandActive)
                    {
                        editorInstance.getAction(COMMENT_ACTION_ID).run();
                        return;
                    }

                    // --- 接下来的所有逻辑都从原始代码中原封不动地复制过来 ---
                    const selection = editorInstance.getSelection();
                    const model = editorInstance.getModel();

                    // 检查是否是普通 JS 注释 (为了 un-comment)
                    const jsCommentRange = new monaco.Range(
                        selection.startLineNumber,
                        model.getLineFirstNonWhitespaceColumn(selection.startLineNumber),
                        selection.startLineNumber,
                        model.getLineMaxColumn(selection.startLineNumber)
                    );
                    const jsCommentText = model.getValueInRange(jsCommentRange);
                    if (jsCommentText.match(/^\s*\/[/*]/))
                    {
                        editorInstance.getAction(COMMENT_ACTION_ID).run();
                        this.resetState(); // 假设 resetState 存在于 this 上下文
                        return;
                    }

                    const runJsxCommentAction = (commentContext: any) =>
                    {
                        let isUnCommentAction = true;
                        const commentsData = [];

                        for (let i = selection.startLineNumber; i <= selection.endLineNumber; i++)
                        {
                            const commentRange = new monaco.Range(
                                i,
                                model.getLineFirstNonWhitespaceColumn(i),
                                i,
                                model.getLineMaxColumn(i)
                            );
                            const commentText = model.getValueInRange(commentRange);
                            commentsData.push({commentRange, commentText});
                            isUnCommentAction = isUnCommentAction && !!commentText.match(/{\/\*/);
                        }

                        // 如果不在 JSX 上下文且不是取消注释操作，则回退到默认行为
                        if (commentContext !== 'JSX' && !isUnCommentAction)
                        { // 假设 commentContext 是一个字符串
                            editorInstance.getAction(COMMENT_ACTION_ID).run();
                            this.resetState();
                            return;
                        }

                        const editOperations = [];
                        let commentsDataIndex = 0;
                        for (let i = selection.startLineNumber; i <= selection.endLineNumber; i++)
                        {
                            let {commentText, commentRange} = commentsData[commentsDataIndex++];
                            if (isUnCommentAction)
                            {
                                commentText = commentText.replace(/{\/\*/, '').replace(/\*\/}/, '');
                            }
                            else
                            {
                                commentText = `{/*${commentText}*/}`;
                            }
                            editOperations.push({
                                range: commentRange,
                                text: commentText,
                                forceMoveMarkers: true
                            });
                        }

                        if (editOperations.length > 0)
                        {
                            editorInstance.executeEdits(this._editorCommandId, editOperations);
                        }
                    };

                    // 假设 runJSXCommentContextAndAction 存在
                    this.runJSXCommentContextAndAction(
                        selection,
                        getAstPromise,
                        onParseErrors,
                        editorInstance,
                        runJsxCommentAction
                    ).catch(onParseErrors);

                }
            );
            this.editorCommandOnDispose = () =>
            {
                this._isJSXCommentCommandActive = false;
            };
            this._isJSXCommentCommandActive = true;
            editorInstance.onDidDispose(this.editorCommandOnDispose);

            return this.editorCommandOnDispose;
        };
    }

    private setupExtraLibs(monaco: MonacoEditor, extraLibs: ExtraLib[]): void {
        if (extraLibs && extraLibs.length > 0)
        {
            const disposables1 = extraLibs.map(lib =>
            {
                console.log(`Injecting extra lib to js: ${lib.filePath}`);
                return monaco.languages.typescript.javascriptDefaults.addExtraLib(
                    lib.content,
                    lib.filePath
                );
            });
            const disposables2 = extraLibs.map(lib =>
            {
                console.log(`Injecting extra lib to ts: ${lib.filePath}`);
                return monaco.languages.typescript.typescriptDefaults.addExtraLib(
                    lib.content,
                    lib.filePath
                );
            });
            this.disposables.push(...disposables1, ...disposables2);
        }
        console.log('Extra libs updated.', this.disposables);
    }



}