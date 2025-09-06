import { type GameObjectBlueprintDTO,type ShapeDTO } from './blueprints/GameObjectBlueprint';
import { ConfigurableGameObject, type CompiledFieldGenerator, type CompiledFieldResponder } from './domain/ConfigurableGameObject';
import { FormulaCompiler } from './utils/FormulaCompiler';
import {type Shape} from './domain/shapes/Shape'
import {RectangleShape} from './domain/shapes/RectangleShape'

export class BlueprintManager {
    private blueprints: Map<string, GameObjectBlueprintDTO> = new Map();

    public loadBlueprint(blueprint: GameObjectBlueprintDTO) {
        this.blueprints.set(blueprint.type, blueprint);
    }

    public createGameObject(type: string, id: string, name: string): ConfigurableGameObject {
        const blueprint = this.blueprints.get(type);
        if (!blueprint) {
            throw new Error(`Blueprint for type "${type}" not found.`);
        }

        // 1. 创建形状 (Shape)
        const shape = this.createShapeFromDTO(blueprint.shape);

        // 2. 编译场生成器
        const generators: CompiledFieldGenerator[] = (blueprint.fieldGenerators || []).map(dto => ({
            fieldName: dto.fieldName,
            calculateForce: FormulaCompiler.compile(dto.forceFormula),
            maxRangeSq: dto.maxRange ? dto.maxRange * dto.maxRange : Infinity
        }));

        // 3. 编译场响应器 (简化版)
        const responders: CompiledFieldResponder[] = (blueprint.fieldResponders || []).map(dto => ({
            fieldName: dto.fieldName,
            responseFactor: typeof dto.responseFactor === 'number' ? dto.responseFactor : 0 // 待扩展
        }));

        // 4. 实例化最终的领域对象
        return new ConfigurableGameObject(
            id,
            name,
            shape,
            blueprint.collision.groups,
            generators,
            responders
        );
    }

    private createShapeFromDTO(dto: ShapeDTO): Shape {
        // ... 实现从DTO创建Shape实例的逻辑
        if (dto.type === 'rectangle') {
            return new RectangleShape(dto.width!, dto.height!);
        }
        throw new Error('Unsupported shape DTO');
    }
}