import {NInput, NInputNumber} from 'naive-ui';
import type {EntityFieldSchema} from '@yaesandbox-frontend/core-services/composables';

/**
 * 角色的实体字段 Schema
 */
export const characterSchema: EntityFieldSchema[] = [
    {path: ['name'], label: '角色名称', component: NInput, rules: {required: true}},
    {path: ['description'], label: '角色描述', component: NInput, componentProps: {type: 'textarea'}},
    {
        path: ['avatar'],
        label: '头像',
        component: NInput,
        componentProps: {
            placeholder: '可填入图片URL、Emoji或1-3个字符，留空则根据名称自动绘制'
        }
    },
];

/**
 * 场景的实体字段 Schema
 */
export const sceneSchema: EntityFieldSchema[] = [
    {path: ['name'], label: '场景名称', component: NInput, rules: {required: true, message: '请输入场景名称'}},
    {path: ['description'], label: '场景描述', component: NInput, componentProps: {type: 'textarea'}},
];

/**
 * 物品的实体字段 Schema
 */
export const itemSchema: EntityFieldSchema[] = [
    {path: ['name'], label: '物品名称', component: NInput, rules: {required: true, message: '请输入名称'}},
    {path: ['description'], label: '物品描述', component: NInput, componentProps: {type: 'textarea'}},
    {
        path: ['price'],
        label: '价格',
        component: NInputNumber,
        dataType: 'number',
        rules: {
            type: 'number',
            required: true,
            message: '请输入价格'
        }
    },
];