# Fridge Management System 🧊

## 📌 Project Overview

The **Fridge Management System (FMS)** is a third-year software development project designed to manage fridges provided by a beverage manufacturing company to its customers. The system ensures proper tracking, allocation, maintenance, and lifecycle management of fridges while maintaining accurate customer and inventory records.

This project simulates a **real-world enterprise system**, focusing on backend logic, system design, and proper separation of responsibilities across subsystems.

## 🎯 Problem Statement

A beverage manufacturer supplies fridges to customers to store its products. Managing these fridges manually leads to:

* Poor tracking of fridge allocations
* Loss of inventory visibility
* Difficulty managing damaged or scrapped fridges
* Inconsistent customer records

The Fridge Management System addresses these challenges by providing a centralized, structured, and scalable solution.

## 🧩 System Scope

The system is divided into multiple subsystems. This repository focuses primarily on the:

### **Customer Management Subsystem**

Responsible for:

* Managing customer profiles
* Allocating fridges to customers
* Tracking fridge ownership per customer
* Handling receiving of new fridges
* Scrapping damaged or old fridges
* Creating fridge purchase requests

The **Administration Subsystem** acts as a secondary actor to oversee and approve selected operations.

## 👥 Actors

* **Customer Manager (Primary Actor)**
* **Administration Subsystem (Secondary Actor)**

## 🔑 Key Features

* Create, update, and view customer records
* Allocate and deallocate fridges to customers
* Track fridge inventory status
* Receive new fridges into the system
* Scrap damaged or obsolete fridges
* Generate purchase requests for new fridges
* Administrative oversight on critical operations

## 🛠️ Technologies Used

* **Backend:** ASP.NET Core (.NET)
* **Frontend:** HTML, CSS, Bootstrap
* **Database:** SQL Server
* **Architecture:** MVC
* **Design Tools:** UML (Use Case Diagrams)

## 📐 System Design

* Modular architecture with clear separation of concerns
* Use Case Diagrams follow standard UML notation
* Business rules enforced at the service layer
* Validation handled through model constraints and annotations

## 📊 Use Case Diagram Focus

The Use Case Diagram in this project:

* Focuses strictly on the **Customer Management Subsystem**
* Places the primary actor on the left
* Includes the Administration Subsystem as a secondary actor on the right
* Excludes supplier, employee, and unrelated admin operations

## 🚀 Learning Outcomes

This project demonstrates:

* Object-Oriented Programming principles
* Real-world system modeling
* Backend-focused application development
* Team collaboration and subsystem ownership
* Practical application of UML and system analysis

## 👨‍💻 Contributors

* **Lebohang Mollo** – Customer Management Subsystem
* Group Members – Other system subsystems

## 📄 License

This project was developed for academic purposes as part of a third-year software development qualification.

⭐ *Feel free to explore the repository and review the system design and implementation.*
Link (School server): "https://soit-iis.mandela.ac.za/grp-03-27"
