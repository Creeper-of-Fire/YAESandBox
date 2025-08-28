// src/main.ts
import { createApp } from 'vue';
import { createPinia } from 'pinia';
// @ts-ignore
import App from './App.vue';
// @ts-ignore
import './styles.css';

const app = createApp(App);
app.use(createPinia());
app.mount('#app');