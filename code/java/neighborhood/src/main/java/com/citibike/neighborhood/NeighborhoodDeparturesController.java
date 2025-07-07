package com.citibike.neighborhood;

import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.stereotype.Controller;
import org.springframework.ui.Model;
import org.springframework.web.bind.annotation.GetMapping;
import java.util.List;
import jakarta.annotation.PostConstruct;

@Controller
public class NeighborhoodDeparturesController {

    private final NeighborhoodDeparturesCountRepository repository;

    @Autowired
    public NeighborhoodDeparturesController(NeighborhoodDeparturesCountRepository repository) {
        this.repository = repository;
    }

    @PostConstruct
    public void init() {
        System.out.println("NeighborhoodDeparturesController init");
    }

    @GetMapping("/departures")
    public String showDeparturesPage(Model model) {
        List<NeighborhoodDeparturesCount> departures = repository.findAll();

        model.addAttribute("departures", departures);

        return "departures";
    }
}
