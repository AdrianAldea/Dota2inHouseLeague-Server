/// Here in app.ts it would "hook" things up accordingly
import { Component } from "@angular/core";
import { AdminPage } from "../pages/admin/admin";

@Component({
    selector: 'app-root',
    standalone: true,
    imports: [AdminPage],
    template: `<admin-page></admin-page>`, // @todo Inlined until we have a router
})

export class App {

}