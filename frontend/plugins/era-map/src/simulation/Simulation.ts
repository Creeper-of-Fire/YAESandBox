import Matter from 'matter-js';
import {GameObject} from './domain/GameObject';
import {CollisionManager} from './physics/CollisionManager';
import {PhysicsAdapter} from './physics/PhysicsAdapter';
import {ConfigurableGameObject} from './domain/ConfigurableGameObject';
import { BlueprintManager } from './BlueprintManager';
import {type SimulationConfigDTO, type SpawnRequestDTO} from './SimulationConfig';

export class Simulation {
    public readonly engine: Matter.Engine;
    private readonly world: Matter.World;

    private readonly blueprintManager: BlueprintManager;
    private readonly collisionManager: CollisionManager;
    private readonly physicsAdapter: PhysicsAdapter;

    private gameObjects: Map<number, ConfigurableGameObject> = new Map();
    // 一个用于缓存上一帧力的 Map
    public forceCache: Map<number, { x: number; y: number }> = new Map();

    /**
     * Simulation 接收一个配置，然后变成那个配置所描述的世界。
     * @param config - 描述整个世界的DTO。
     */
    constructor(config: SimulationConfigDTO) {
        this.engine = Matter.Engine.create();
        this.world = this.engine.world;

        this.engine.gravity.x = 0;
        this.engine.gravity.y = 0;

        // 1. 碰撞管理器首先从蓝图中学习世界的规则。
        this.collisionManager = new CollisionManager(config.blueprints);

        // 2. 蓝图管理器加载所有蓝图以备后用。
        this.blueprintManager = new BlueprintManager();
        config.blueprints.forEach(bp => this.blueprintManager.loadBlueprint(bp));

        // 3. 物理适配器准备就绪。
        this.physicsAdapter = new PhysicsAdapter(); // 它不再需要知道 group manager

        // 4. 根据生成请求，在世界中创建所有初始对象。
        this.spawnInitialObjects(config.spawnRequests);
    }

    private spawnInitialObjects(spawnRequests: SpawnRequestDTO[]): void {
        for (const request of spawnRequests) {
            const gameObject = this.blueprintManager.createGameObject(
                request.blueprintType,
                request.id,
                request.name
            );
            this.addGameObject(request.initialPosition.x, request.initialPosition.y, gameObject);
        }
    }
    /**
     * 在物理引擎清空力之前，将它们缓存起来。
     */
    private cacheForces(bodies: Matter.Body[]) {
        this.forceCache.clear();
        for (const body of bodies) {
            // 必须创建一个新对象，因为 body.force 本身会被修改
            this.forceCache.set(body.id, { x: body.force.x, y: body.force.y });
        }
    }
    /**
     * 根据传入的参数，动态计算并返回一个势场网格。
     * 这个方法是无状态的，它不修改 Simulation 实例的任何属性。
     * @param gridWidth - 网格的宽度（单元格数量）
     * @param gridHeight - 网格的高度（单元格数量）
     * @param cellSize - 每个单元格的像素尺寸
     * @returns 一个二维数组，代表计算出的势场
     */
    public calculatePotentialGrid(gridWidth: number, gridHeight: number, cellSize: number): number[][] {
        const grid: number[][] = Array(gridHeight).fill(0).map(() => Array(gridWidth).fill(0));
        const bodies = this.getAllBodies();

        if (bodies.length === 0) {
            return grid;
        }
        for (let i = 0; i < gridHeight; i++) {
            for (let j = 0; j < gridWidth; j++) {
                const worldX = (j + 0.5) * cellSize;
                const worldY = (i + 0.5) * cellSize;

                let totalPotential = 0;

                for (const body of bodies) {
                    const obj = this.getGameObjectByBodyId(body.id) as ConfigurableGameObject;
                    if (!obj) continue;

                    const dx = body.position.x - worldX;
                    const dy = body.position.y - worldY;
                    const distSq = dx * dx + dy * dy;

                    if (distSq < 1) continue; // 避免在点源内部计算
                    const dist = Math.sqrt(distSq);

                    for (const generator of obj.fieldGenerators) {
                        if (distSq < generator.maxRangeSq) {
                            totalPotential += Math.abs(generator.calculateForce(dist));
                        }
                    }
                }
                grid[i][j] = totalPotential;
            }
        }
        return grid;
    }

    public addGameObject(x: number, y: number, gameObject: ConfigurableGameObject): void {
        // 为这个具体的实例获取它应有的碰撞过滤器
        const filter = this.collisionManager.getCollisionFilter(gameObject.collisionGroups);

        const body = this.physicsAdapter.createBody(x, y, gameObject, filter);

        this.gameObjects.set(body.id, gameObject);
        Matter.World.add(this.world, body);
    }

    public update(deltaTime: number) {
        // 1. 应用自定义的场力
        const bodies = this.getAllBodies();

        // 性能考量: 如果物体数量巨大，这个 O(n^2) 循环会是瓶颈。
        // 但对于几百个物体，这是完全可以接受的。
        for (let i = 0; i < bodies.length; i++) {
            for (let j = i + 1; j < bodies.length; j++) {
                const bodyA = bodies[i];
                const bodyB = bodies[j];

                // 如果两个物体都是静态的，没必要计算它们之间的力
                if (bodyA.isStatic && bodyB.isStatic) {
                    continue;
                }

                const objA = this.getGameObjectByBodyId(bodyA.id) as ConfigurableGameObject;
                const objB = this.getGameObjectByBodyId(bodyB.id) as ConfigurableGameObject;

                // 确保我们能找到对应的游戏对象
                if (!objA || !objB) {
                    continue;
                }

                this.applyForcesBetween(objA, bodyA, objB, bodyB);
            }
        }

        // 在 update 之前，缓存所有物体的力
        this.cacheForces(bodies);

        // 2. 更新物理引擎（包含了碰撞检测、约束求解等）
        Matter.Engine.update(this.engine, deltaTime);
    }

    public getAllBodies(): Matter.Body[]
    {
        return Matter.Composite.allBodies(this.world);
    }

    public getGameObjectByBodyId(id: number): GameObject | undefined
    {
        return this.gameObjects.get(id);
    }

    /**
     * 计算并应用两个游戏对象之间的自定义场力。
     * 这是一个对称的过程，我们会计算 A->B 和 B->A 的所有交互。
     * @param objA - 第一个游戏对象
     * @param bodyA - 对应的 Matter.Body
     * @param objB - 第二个游戏对象
     * @param bodyB - 对应的 Matter.Body
     */
    private applyForcesBetween(
        objA: ConfigurableGameObject,
        bodyA: Matter.Body,
        objB: ConfigurableGameObject,
        bodyB: Matter.Body
    ) {
        const dx = bodyB.position.x - bodyA.position.x;
        const dy = bodyB.position.y - bodyA.position.y;

        const distSq = dx * dx + dy * dy;

        // 避免在距离为0时计算，防止除以零的错误
        if (distSq < 0.0001) {
            return;
        }

        const dist = Math.sqrt(distSq);

        let forceMagnitudeOnB = 0;
        let forceMagnitudeOnA = 0;

        // --- 1. 计算 A 施加在 B 上的力 ---
        let forceMagnitudeFromAtoB = 0;
        for (const generator of objA.fieldGenerators) {
            const responder = objB.fieldResponders.find(r => r.fieldName === generator.fieldName);
            if (responder && distSq < generator.maxRangeSq) {
                forceMagnitudeFromAtoB += generator.calculateForce(dist) * responder.responseFactor;
            }
        }

        // --- 2. 计算 B 施加在 A 上的力 ---
        let forceMagnitudeFromBtoA = 0;
        for (const generator of objB.fieldGenerators) {
            const responder = objA.fieldResponders.find(r => r.fieldName === generator.fieldName);
            if (responder && distSq < generator.maxRangeSq) {
                forceMagnitudeFromBtoA += generator.calculateForce(dist) * responder.responseFactor;
            }
        }

        // --- 3. 如果两个方向的力都几乎为零，则提前退出 ---
        if (Math.abs(forceMagnitudeFromAtoB) < 1e-6 && Math.abs(forceMagnitudeFromBtoA) < 1e-6) {
            return;
        }

        // 计算归一化的方向向量 (从 A 指向 B)
        const normalX = dx / dist;
        const normalY = dy / dist;

        // A 对 B 施加的力
        if (Math.abs(forceMagnitudeFromAtoB) > 1e-6 && !bodyB.isStatic) {
            const forceOnB = {
                x: forceMagnitudeFromAtoB * normalX,
                y: forceMagnitudeFromAtoB * normalY,
            };
            Matter.Body.applyForce(bodyB, bodyB.position, forceOnB);
        }

        // B 对 A 施加的力
        if (Math.abs(forceMagnitudeFromBtoA) > 1e-6 && !bodyA.isStatic) {
            // 力的大小是 forceMagnitudeFromBtoA
            // 方向是从 B 指向 A，也就是单位向量的负方向
            const forceOnA = {
                x: forceMagnitudeFromBtoA * -normalX,
                y: forceMagnitudeFromBtoA * -normalY,
            };
            Matter.Body.applyForce(bodyA, bodyA.position, forceOnA);
        }
    }
}