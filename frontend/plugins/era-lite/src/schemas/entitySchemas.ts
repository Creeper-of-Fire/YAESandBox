import {NInput, NInputNumber} from 'naive-ui';
import type {EntityFieldSchema} from '#/types/entitySchema';

/**
 * 角色的实体字段 Schema
 */
export const characterSchema: EntityFieldSchema[] = [
    {key: 'name', label: '角色名称', component: NInput, rules: {required: true}},
    {key: 'description', label: '角色描述', component: NInput, componentProps: {type: 'textarea'}},
    {key: 'avatar', label: '头像 (Emoji)', component: NInput, rules: {required: true}},
];

/**
 * 场景的实体字段 Schema
 */
export const sceneSchema: EntityFieldSchema[] = [
    {key: 'name', label: '场景名称', component: NInput, rules: {required: true, message: '请输入场景名称'}},
    {key: 'description', label: '场景描述', component: NInput, componentProps: {type: 'textarea'}},
];

/**
 * 物品的实体字段 Schema
 */
export const itemSchema: EntityFieldSchema[] = [
    {key: 'name', label: '物品名称', component: NInput, rules: {required: true, message: '请输入名称'}},
    {key: 'description', label: '物品描述', component: NInput, componentProps: {type: 'textarea'}},
    {key: 'price', label: '价格', component: NInputNumber, rules: {type: 'number', required: true, message: '请输入价格'}},
];