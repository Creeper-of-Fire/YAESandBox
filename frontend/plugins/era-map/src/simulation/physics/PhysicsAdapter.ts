import Matter from 'matter-js';
import { GameObject } from '../domain/GameObject';
// import { CircleShape } from '../domain/shapes/CircleShape'; // 假设我们未来会添加圆形
// import { PolygonShape } from '../domain/shapes/PolygonShape'; // 以及多边形
import { RectangleShape } from '../domain/shapes/RectangleShape';

/**
* PhysicsAdapter 负责将我们抽象的领域模型对象（GameObject）
* 转换为具体的物理引擎实体（Matter.Body）。
* 它是一个无状态的服务，不存储任何信息，只进行转换。
*/
export class PhysicsAdapter {
    /**
     * @param x - 初始 x 坐标
     * @param y - 初始 y 坐标
     * @param gameObject - 我们要为其创建物理实体的领域对象
     * @param filter - 由 CollisionManager 计算出的 category 和 mask
     * @returns 一个配置好的 Matter.Body 实例
     */
    public createBody(
        x: number,
        y: number,
        gameObject: GameObject,
        filter: { category: number; mask: number }
    ): Matter.Body {

        // 1. 准备所有形状通用的选项
        const commonOptions: Matter.IBodyDefinition = {
            ...this.getPhysicsOptions(gameObject),
            collisionFilter: filter,
        };

        // 2. 将创建的最终责任委托给 shape 对象本身。
        //    这里就是多态的魔力。我们不需要知道 shape 是什么类型，
        //    我们只知道它肯定有一个 createBody 方法。
        const body = gameObject.shape.createBody(x, y, commonOptions);

        // 3. 关联ID以便反向查找
        body.label = gameObject.id;

        return body;
    }

    /**
     * 一个辅助方法，用于从 GameObject 中提取或推断物理属性。
     * 未来可以从蓝图 DTO 中读取更详细的配置。
     */
    private getPhysicsOptions(gameObject: GameObject): Matter.IBodyDefinition {
        const options: Matter.IBodyDefinition = {
            friction: 0.1,
            frictionAir: 0.01,
            restitution: 0.2, // 弹性
        };

        // 允许 GameObject 自定义其物理属性，例如墙壁是静态的
        if (typeof (gameObject as any).getPhysicsOptions === 'function') {
            Object.assign(options, (gameObject as any).getPhysicsOptions());
        }

        return options;
    }
}