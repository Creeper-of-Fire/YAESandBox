import {type Component, defineAsyncComponent, markRaw} from 'vue';

/**
 * 创建一个懒加载、且被 markRaw 标记的图标组件。
 * @param loader 一个返回动态 import() 的函数。
 */
const createIcon = (loader: () => Promise<any>): Component =>
{
    // 1. defineAsyncComponent 会创建一个异步（懒加载）组件
    // 2. markRaw 包裹整个异步组件定义，告诉 Vue 不要尝试让这个定义本身变成响应式
    return markRaw(defineAsyncComponent(loader));
};

// 统一图标库
export const GameControllerIcon = createIcon(() => import('@vicons/ionicons5/es/GameControllerOutline'));
export const CloseIcon = createIcon(() => import('@vicons/ionicons5/es/CloseOutline'));
export const InfoIcon = createIcon(() => import('@vicons/ionicons5/es/InformationCircleOutline'));
export const AddBoxIcon = createIcon(() => import('@vicons/material/es/AddBoxOutlined'));
export const SwapHorizIcon = createIcon(() => import('@vicons/material/es/SwapHorizOutlined'));
export const PeopleIcon = createIcon(() => import('@vicons/ionicons5/es/PeopleOutline'));
export const EditIcon = createIcon(() => import('@vicons/material/es/EditOutlined'));
export const LinkOffIcon = createIcon(() => import('@vicons/material/es/LinkOffOutlined'));
export const FindInPageIcon = createIcon(() => import('@vicons/material/es/FindInPageOutlined'));
export const SaveIcon = createIcon(() => import('@vicons/material/es/SaveOutlined'));
export const KeyboardArrowDownIcon = createIcon(() => import('@vicons/material/es/KeyboardArrowDownFilled'));
export const KeyboardArrowUpIcon = createIcon(() => import('@vicons/material/es/KeyboardArrowUpFilled'));
export const ArrowForwardIcon = createIcon(() => import('@vicons/material/es/ArrowForwardOutlined'));
export const DeleteIcon = createIcon(() => import('@vicons/material/es/DeleteOutlineRound'));
export const SettingsIcon = createIcon(() => import('@vicons/ionicons5/es/SettingsOutline'));
export const DragHandleOutlined = createIcon(() => import('@vicons/material/es/DragHandleOutlined'));
export const AddIcon = createIcon(() => import('@vicons/material/es/AddCircleOutlineRound'));
export const ListIcon = createIcon(() => import('@vicons/ionicons5/es/ListOutline'));
export const HelpCircleIcon = createIcon(() => import('@vicons/ionicons5/es/HelpCircleOutline'));
export const AlertCircleIcon = createIcon(() => import('@vicons/ionicons5/es/AlertCircleOutline'));
export const WorkflowIcon = createIcon(() => import('@vicons/material/es/AccountTreeOutlined'));
export const TuumIcon = createIcon(() => import('@vicons/material/es/HubOutlined'));
export const TrashIcon = createIcon(() => import('@vicons/ionicons5/es/TrashBinOutline'));
export const EllipsisHorizontalIcon = createIcon(() => import('@vicons/ionicons5/es/EllipsisHorizontal'));
export const ChevronDownIcon = createIcon(() => import('@vicons/ionicons5/es/ChevronDownOutline'));
export const PricetagIcon = createIcon(() => import('@vicons/ionicons5/es/PricetagOutline'));
export const LinkIcon = createIcon(() => import('@vicons/ionicons5/es/LinkOutline'));
export const DocumentTextIcon = createIcon(() => import('@vicons/ionicons5/es/DocumentTextOutline'));
export const CloudUploadIcon = createIcon(() => import('@vicons/ionicons5/es/CloudUploadOutline'));
export const CloudDownloadIcon = createIcon(() => import('@vicons/ionicons5/es/CloudDownloadOutline'));
export const ChevronLeftIcon = createIcon(() => import('@vicons/material/es/ChevronLeftOutlined'));
export const ChevronRightIcon = createIcon(() => import('@vicons/material/es/ChevronRightOutlined'));
export const RefreshIcon = createIcon(() => import('@vicons/ionicons5/es/Refresh'));
export const CodeIcon = createIcon(() => import('@vicons/ionicons5/es/CodeSlashOutline'));
export const FullscreenExitIcon = createIcon(() => import('@vicons/material/es/FullscreenExitOutlined'));
export const FullscreenIcon = createIcon(() => import('@vicons/material/es/FullscreenOutlined'));
export const LoginIcon = createIcon(() => import('@vicons/ionicons5/es/LogInOutline'));
export const HubIcon = createIcon(() => import('@vicons/material/es/HubOutlined'));
// export {default as }from'@vicons/ionicons5/es/';