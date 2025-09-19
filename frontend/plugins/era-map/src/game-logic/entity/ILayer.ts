import type {Component} from "vue";

export interface ILayer
{
    // 这个方法是核心：它返回一个可被Vue渲染的组件
    getRendererComponent(): Component;
}