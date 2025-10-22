import type {AnyConfigObject} from "@yaesandbox-frontend/core-services/types";

/**
 * 在配置对象树中递归查找指定对象的引用，并返回其路径。
 * @param root - 要搜索的根配置对象。
 * @param targetObject - 要查找的对象引用。
 * @returns 返回对象的路径字符串 (e.g., "tuums[0].runes[1]"), 如果找不到则返回 undefined。
 */
export function findPathByReference(root: AnyConfigObject, targetObject: AnyConfigObject): string | undefined {
    function search(current: any, path: string): string | undefined {
        if (current === targetObject) {
            return path;
        }

        if (typeof current !== 'object' || current === null) {
            return undefined;
        }

        if (Array.isArray(current)) {
            for (let i = 0; i < current.length; i++) {
                const result = search(current[i], `${path}[${i}]`);
                if (result) return result;
            }
        } else {
            const keysToSearch = ['tuums', 'runes', 'innerTuum'];
            for (const key of keysToSearch) {
                if (key in current) {
                    const newPath = path ? `${path}.${key}` : key;
                    const result = search(current[key], newPath);
                    if (result) return result;
                }
            }
        }
        return undefined;
    }

    // 对于根对象，直接返回空路径
    if (root === targetObject) {
        return '';
    }

    return search(root, '');
}