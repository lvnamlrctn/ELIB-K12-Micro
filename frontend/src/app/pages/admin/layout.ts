import { Component, inject, OnInit } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { LayoutService } from '../../services/layout';
import { Sidebar } from '../../components/sidebar/sidebar';
import { Header } from '../../components/header/header';
import { Footer } from '../../components/footer/footer';
import { PermissionService } from '../../services/system/permission.service';
import { AdminStatChatComponent } from '../../components/admin-stat-chat/admin-stat-chat.component';

@Component({
  selector: 'app-admin-layout',
  standalone: true,
  imports: [RouterOutlet, Sidebar, Header, Footer, AdminStatChatComponent],
  templateUrl: './layout.html'
})
export class Layout implements OnInit {
  layout = inject(LayoutService);
  private permission = inject(PermissionService);

  ngOnInit() {
    // Tải quyền 1 lần khi vào admin (bao trùm cả F5); cache theo userId trong service.
    this.permission.load().subscribe();
  }
}

