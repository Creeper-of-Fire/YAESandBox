import { injectionMap } from './injection';
import * as icons from '@yaesandbox-frontend/shared-ui/icons';

// 为了更精确地识别图标，我们可以创建一个 Set
const iconNames = new Set(Object.keys(icons));

/**
 * 遍历 injectionMap 并生成一个 TypeScript 声明文件 (.d.ts) 的内容。
 * 这将为 Monaco 编辑器提供全局变量的类型信息和文档。
 */
function generateDtsFromInjectionMap(): string {
    let dtsContent = `
/**
 * =========================================================================
 * YAE Sandbox - 全局注入的变量和组件
 *
 * 以下类型是自动生成的，用于在 JSX 编辑器中提供智能提示。
 * 请勿直接编辑此文件。
 * =========================================================================
 */
 
`;

    for (const [key, value] of Object.entries(injectionMap)) {
        let docComment = '/**\n * @description ';
        let type = 'any'; // 默认使用 any 类型，保证灵活性

        // 规则 1: 判断是否是组件 (变量名以大写字母开头)
        // 这个规则应该优先，因为它最明确。
        if (key[0] >= 'A' && key[0] <= 'Z' && key !== 'Vue') {
            type = 'Vue.Component'; // 提供一个更具体的类型
            if (iconNames.has(key)) {
                docComment += `(ICON) ${key} 图标组件。`;
            } else {
                docComment += `(COMPONENT) ${key} UI 组件。`;
            }
        }
        // 规则 2: 判断是否是 Hook/Composable (以 'use' 开头)
        else if (key.startsWith('use') && typeof value === 'function') {
            docComment += `(HOOK) ${key} Vue Composable。必须在 setup 函数上下文中使用。`;
        }
        // 规则 3: 判断 Vue 核心对象
        else if (key === 'Vue') {
            docComment += `(FRAMEWORK) Vue 核心对象 (ref, computed, h, etc.)。`;
            // 我们可以为 Vue 提供一个更丰富的类型定义
            type = `{ h: Function, Fragment: any, ref: Function, computed: Function, watch: Function, defineComponent: Function, [key: string]: any }`;
        }
        // 规则 4: 判断其他函数
        else if (typeof value === 'function') {
            docComment += `(FUNCTION) ${key} 函数。`;
        }
        // 规则 5: 其他对象或变量
        else if (typeof value === 'object' && value !== null) {
            docComment += `(OBJECT) ${key} 导入的对象/模块。`;
        } else {
            docComment += `(VARIABLE) ${key} 注入的变量。`;
        }

        docComment += '\n */';
        dtsContent += `${docComment}\ndeclare const ${key}: ${type};\n\n`;
    }

    return dtsContent;
}

// 导出生成的 .d.ts 字符串，以便在 SmartEditor 中使用
export const injectionDtsContent = generateDtsFromInjectionMap();

// 导出一个虚拟的文件名，方便管理
export const injectionDtsPath = 'ts:filename/sandbox-injections.d.ts';