import { type Shape } from './Shape';
import Matter from 'matter-js';

// Shape 接口的一个具体实现
export class RectangleShape implements Shape {
    public readonly type = 'rectangle';
    public width: number;
    public height: number;

    constructor(width: number, height: number) {
        this.width = width;
        this.height = height;
    }

    public createBody(x: number, y: number, options: Matter.IBodyDefinition): Matter.Body {
        const rectangleOptions: Matter.IChamferableBodyDefinition = {
            ...options,
            // 然后显式地将 chamfer 属性设置为 undefined。
            //    这就向 TypeScript 编译器明确表示：我知道这个属性的存在，
            //    并且我确定它的值是 undefined（即不使用倒角功能）。
            chamfer: undefined,
        };

        return Matter.Bodies.rectangle(x, y, this.width, this.height, rectangleOptions);
    }

    public getRenderConfig(body: Matter.Body): { component: string; config: Record<string, any> } {
        return {
            component: 'v-rect', // 告诉渲染器使用 <v-rect>
            config: {
                x: body.position.x,
                y: body.position.y,
                width: this.width,
                height: this.height,
                rotation: body.angle * (180 / Math.PI),
                offsetX: this.width / 2,
                offsetY: this.height / 2,
                fill: 'saddlebrown',
                stroke: 'black',
                strokeWidth: 2,
            }
        };
    }
}