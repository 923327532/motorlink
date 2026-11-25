// MotorLink · punto de entrada. Cada módulo se inicializa solo si su marcado existe en la página.
import { initIcons } from './ui/icons.js';
import { initToasts } from './ui/toast.js';
import { initConfirm } from './ui/confirm.js';
import { initLoading } from './ui/loading.js';
import { initModalFill } from './ui/modal-fill.js';
import { initForms } from './features/forms.js';
import { initFavorites } from './features/favorites.js';
import { initCascades } from './features/cascade.js';
import { initMarketplace } from './features/marketplace.js';
import { initGallery } from './features/gallery.js';
import { initListingActions } from './features/listing-actions.js';
import { initCharts } from './features/charts.js';
import { initNotifications } from './features/notifications.js';
import { initWizard } from './features/wizard.js';

function start() {
    initToasts();
    initConfirm();
    initLoading();
    initModalFill();
    initForms();
    initFavorites();
    initCascades();
    initMarketplace();
    initGallery();
    initListingActions();
    initCharts();
    initNotifications();
    initWizard();
    initIcons();
}

if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', start); else start();
