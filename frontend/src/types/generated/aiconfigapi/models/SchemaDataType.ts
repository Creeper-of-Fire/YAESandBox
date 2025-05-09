/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
/**
 * 定义了Schema字段支持的主要数据类型，供前端进行UI渲染决策。
 * String, // 普通字符串
 * Number, // 包含整数和浮点数
 * Boolean, // 布尔值 (true/false)
 * Enum, // 枚举类型，通常配合 Options 使用
 * Object, // 嵌套的复杂对象，其结构由 NestedSchema 定义
 * Array, // 数组/列表，其元素结构由 ArrayItemSchema 定义
 * MultilineText, // 多行文本输入 (textarea)
 * Password, // 密码输入框
 * Integer, // 专指整数
 * DateTime, // 日期或日期时间
 * GUID, // GUID 全局唯一标识符
 * Dictionary, // 字典/映射类型，键信息由 KeyInfo 定义，值结构由 DictionaryValueSchema 定义
 * Unknown // 未知或不支持的类型
 */
export enum SchemaDataType {
    STRING = 'String',
    NUMBER = 'Number',
    BOOLEAN = 'Boolean',
    ENUM = 'Enum',
    OBJECT = 'Object',
    ARRAY = 'Array',
    MULTILINE_TEXT = 'MultilineText',
    PASSWORD = 'Password',
    INTEGER = 'Integer',
    DATE_TIME = 'DateTime',
    GUID = 'GUID',
    DICTIONARY = 'Dictionary',
    UNKNOWN = 'Unknown',
}
