import type {Component} from "vue";
import type {IGameEntity} from "./IGameEntity";

/**
 * 代表一个可被渲染的图层。
 */
export interface ILayer
{
    // 返回一个可被Vue渲染的组件
    getRendererComponent(): Component;
}