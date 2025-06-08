import type {RouteRecordRaw} from 'vue-router';
import GameModeView from "@/app-game/GameModeView.vue";

export const routes: RouteRecordRaw[] = [
    {
        path: '/game',
        name: 'game',
        component: GameModeView,
        children: [],
    },
];