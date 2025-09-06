import type {GameObjectBlueprintDTO} from "#/simulation/blueprints/GameObjectBlueprint.ts";

export class CollisionManager {
    private groupToCategory: Map<string, number> = new Map();

    /**
     * 这是整个系统的核心。
     * 它分析所有提供的蓝图，自动发现所有唯一的碰撞组，
     * 并为它们分配唯一的物理类别。
     * @param blueprints - 从 SimulationConfigDTO 传入的所有蓝图。
     */
    constructor(blueprints: GameObjectBlueprintDTO[]) {
        console.log("CollisionManager: Initializing and discovering groups...");
        const allGroups = new Set<string>();

        // 1. 发现 (Discover)
        for (const blueprint of blueprints) {
            if (blueprint.collision?.groups) {
                for (const group of blueprint.collision.groups) {
                    allGroups.add(group);
                }
            }
        }

        // 2. 分配 (Assign)
        let nextCategoryBit = 0;
        allGroups.forEach(groupName => {
            const category = 1 << nextCategoryBit++; // 1, 2, 4, 8...
            this.groupToCategory.set(groupName, category);
            console.log(`Discovered and assigned category for group "${groupName}": ${category}`);
        });
    }

    /**
     * 为一个对象，根据它声明的组，计算出它的 category 和 mask。
     * 这是魔法发生的地方。
     * @param objectGroups - 对象声明的组列表, e.g., ["FURNITURE", "SOLID"]
     */
    public getCollisionFilter(objectGroups: string[]): { category: number; mask: number } {
        // Category 是这个对象所有组的位掩码的总和。
        // 它代表了“我是谁”。
        let category = 0;
        for (const group of objectGroups) {
            category |= this.getCategoryBit(group);
        }

        // Mask 也是这个对象所有组的位掩码的总和。
        // 它代表了“我与谁交互”。
        // 在这个模型中，如果你属于某个组，你就会与所有也属于该组的物体交互。
        // 因此，category 和 mask 是完全相同的。
        const mask = category;

        return { category, mask };
    }

    private getCategoryBit(groupName: string): number {
        return this.groupToCategory.get(groupName) ?? 0;
    }
}