import Matter from 'matter-js';
import  type {Shape, Vertex } from './Shape';


export class PolygonShape implements Shape {
    public readonly type = 'polygon';
    public vertices: Vertex[];

    constructor(vertices: Vertex[]) {
        this.vertices = vertices;
    }

    public createBody(x: number, y: number, options: Matter.IBodyDefinition): Matter.Body {
        // 多边形创建逻辑被完美地封装在这里。
        const body = Matter.Bodies.fromVertices(x, y, [this.vertices], options);
        // 创建后可能需要调整位置，因为 fromVertices 的行为可能与预期不符
        Matter.Body.setPosition(body, { x, y });
        return body;
    }

    public getRenderConfig(body: Matter.Body): { component: string; config: Record<string, any> } {
        // 多边形在 Konva 中通常用 <v-line> 并闭合路径来渲染。
        // matter.js 的顶点是 {x, y} 对象数组，Konva 需要一个扁平的数字数组 [x1, y1, x2, y2, ...]。
        // 顶点是相对于物体中心的，所以我们只需要设置 group 的 x, y, rotation。
        const points = body.vertices.flatMap(v => [v.x, v.y]);

        return {
            component: 'v-line', // 告诉渲染器使用 <v-line>
            config: {
                x: body.position.x,
                y: body.position.y,
                points: body.vertices.flatMap(v => [v.x - body.position.x, v.y - body.position.y]),
                rotation: body.angle * (180 / Math.PI),
                closed: true, // 确保多边形是闭合的
                fill: 'darkgrey',
                stroke: 'black',
                strokeWidth: 4,
            }
        };
    }
}