import { type GameObjectBlueprintDTO } from './blueprints/GameObjectBlueprint';

export interface SpawnRequestDTO {
    blueprintType: string;
    id: string;
    name: string;
    initialPosition: { x: number; y: number };
}

export interface SimulationConfigDTO {
    blueprints: GameObjectBlueprintDTO[];
    spawnRequests: SpawnRequestDTO[];
}