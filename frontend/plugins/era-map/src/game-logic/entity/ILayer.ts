import type {Component} from "vue";

/**
 * 代表一个可被渲染的图层。
 */
export interface ILayer
{
    layerType:string;
    // 返回一个可被Vue渲染的组件
    getRendererComponent(): Component;
}