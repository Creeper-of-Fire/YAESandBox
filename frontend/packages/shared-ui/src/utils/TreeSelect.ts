import {type TreeSelectOverrideNodeClickBehavior} from "naive-ui";

export const TreeSelectOverride: TreeSelectOverrideNodeClickBehavior = ({option}) =>
{
    if (option.children)
    {
        return 'toggleExpand'
    }
    return 'default'
}