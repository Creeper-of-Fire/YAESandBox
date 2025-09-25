import { type MonacoEditor } from '@guolao/vue-monaco-editor';

// 定义 extraLibs 的标准接口
export interface ExtraLib {
    content: string;
    filePath: string;
}

// 定义所有语言增强器必须遵守的契约
export interface MonacoLanguageEnhancer
{
    /**
     * 在 Monaco 编辑器挂载后执行，用于应用所有语言特有的增强功能。
     * @param editor - Monaco 编辑器实例
     * @param monaco - Monaco API 实例
     * @param extraLibs - 从外部传入的额外类型库
     */
    setup(editor: any, monaco: MonacoEditor, extraLibs: ExtraLib[]): void;

    /**
     * 在编辑器销毁前执行，用于清理所有添加的命令、事件监听和资源。
     */
    dispose(): void;
}