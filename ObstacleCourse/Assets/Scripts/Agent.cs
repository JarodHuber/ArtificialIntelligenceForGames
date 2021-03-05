﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public enum AIDifficulty { Beginner, Intermediate, Expert }
public class Agent : MonoBehaviour
{
    public enum State { STOP, MOVETOOBSTACLE, JUMP, SKINNY, PUSHER, TRAPDOOR, WRECKER }

    #region Variables
    public AIDifficulty difficulty = AIDifficulty.Beginner;
    // Array with nodes for the start of each obstacle
    public Node[] baseNodes;
    [Space(10)]
    public float stoppingDistance = 1f;
    [Space(10)]
    public float speed = 10f;
    public float jumpForce = 7f;
    public float jumpSpread = 0f;
    public float gravity = 20f;

    public Queue<Node> path = new Queue<Node>();

    public Node target = null;

    NavMeshAgent agent = null;
    Rigidbody rb = null;

    float dist = float.MaxValue;
    int curObstacle = 0;

    public State curState = State.MOVETOOBSTACLE;

    bool jumpStarted = false;
    Vector3 vel = new Vector3();
    #endregion

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        rb = GetComponent<Rigidbody>();
        target = baseNodes[curObstacle];
    }

    void Update()
    {
        switch (curState)
        {
            case State.STOP:
                break;
            case State.MOVETOOBSTACLE:
                MoveToObstacle();
                break;
            case State.JUMP:
                Jump();
                break;
            case State.SKINNY:
                Skinny();
                break;
            case State.PUSHER:
                break;
            case State.TRAPDOOR:
                break;
            case State.WRECKER:
                break;
        }
    }

    void MoveToObstacle()
    {
        dist = Vector3.Distance(transform.position, target.transform.position);

        if (dist > stoppingDistance)
        {
            agent.destination = target.transform.position;

            //if(agent.pathStatus != NavMeshPathStatus.PathComplete)
            //{
            //    --curObstacle;
            //    if (curObstacle < 0)
            //        curObstacle = 0;
            //}

            return;
        }

        if (dist <= stoppingDistance)
        {
            agent.destination = target.transform.position + ((transform.position - target.transform.position).normalized * stoppingDistance);

            if (curObstacle == baseNodes.Length-1)
            {
                agent.isStopped = true;
                curState = State.STOP;

                return;
            }

            agent.enabled = false;
            rb.isKinematic = false;
            curState = (State)(curObstacle + 2);
            StartObstacle();
        }
    }

    void Jump()
    {
        if (!jumpStarted)
        {
            //float jForceMod = Vector3.Distance(target.transform.position, ((DijkNode)path.Peek()).transform.position) / (speed * 0.65f);

            //vel = new Vector3(Random.Range(-1f, 1f) * jumpSpread, jumpForce, speed * jForceMod);
            float jForceMod = Vector3.Distance(transform.position, target.transform.position) / (speed * 0.65f);

            vel = new Vector3(Random.Range(-1f, 1f) * jumpSpread, jumpForce, speed * jForceMod);

            vel = Quaternion.LookRotation(target.transform.position - transform.position, Vector3.up) * vel;
        }

        vel.y -= gravity * Time.deltaTime;
        rb.velocity = vel;

        if(jumpStarted && IsGrounded())
        {
            jumpStarted = false;
            rb.velocity = Vector3.zero;

            if(transform.position.y < -1)
            {
                print("failed");

                target = baseNodes[curObstacle];
                rb.isKinematic = true;
                agent.enabled = true;
                curState = State.MOVETOOBSTACLE;
                return;
            }

            if (path.Count == 0)
            {
                print("finished");

                target = baseNodes[++curObstacle];
                rb.isKinematic = true;
                agent.enabled = true;
                curState = State.MOVETOOBSTACLE;
                return;
            }

            target = (DijkNode)path.Dequeue();

            return;
        }

        jumpStarted = true;
    }

    void Skinny()
    {
        dist = Vector3.Distance(transform.position, target.transform.position);


    }

    private void OnDrawGizmos()
    {
        if (curState != State.MOVETOOBSTACLE)
        {
            Queue<Node> tmpQueue = new Queue<Node>(path);
            Vector3 targ = target.transform.position;

            Debug.DrawLine(transform.position, target.transform.position, Color.magenta);

            while (tmpQueue.Count != 0)
            {
                Debug.DrawLine(targ, tmpQueue.Peek().transform.position, Color.magenta);
                targ = tmpQueue.Dequeue().transform.position;
            }
        }
    }

    void StartObstacle()
    {
        NavNode node = (NavNode)baseNodes[curObstacle];
        bool takeExpert = DifficultyCheck();
        switch (curObstacle)
        {
            case 0: // Jump
                path = CalculatePath((DijkNode)node.nextNode[0], (DijkNode)node.nextNode[node.nextNode.Length - 1], takeExpert);
                break;
            case 1: // Skinny
                path = new Queue<Node>();
                for (int x = 0; x < node.nextNode.Length; ++x)
                {
                    path.Enqueue(node.nextNode[x]);
                }
                break;
            case 2: // Pusher
                path = new Queue<Node>();
                if (takeExpert)
                {
                    for (int x = 5; x < node.nextNode.Length; ++x)
                    {
                        path.Enqueue(node.nextNode[x]);
                    }

                    break;
                }

                for (int x = 0; x < 5; ++x)
                {
                    path.Enqueue(node.nextNode[x]);
                }
                break;
            case 3: // Trap
                path = CalculatePath((DijkNode)node.nextNode[0], (DijkNode)node.nextNode[node.nextNode.Length-1]);
                break;
            case 4: // Wrecker
                path = new Queue<Node>();
                for (int x = 0; x < node.nextNode.Length; ++x)
                {
                    path.Enqueue(node.nextNode[x]);
                }
                break;
        }

        target = path.Dequeue();
    }

    Queue<Node> CalculatePath(DijkNode startNode, DijkNode targetNode, bool calcShortest = true, bool useAStar = true)
    {
        List<DijkNode> openList = new List<DijkNode>();
        List<DijkNode> closedList = new List<DijkNode>();

        openList.Add(startNode);
        openList[0].SetScores(targetNode);

        while (openList.Count != 0)
        {
            if(openList[0] == targetNode)
            {
                break;
            }

            for (int x = 0; x < openList[0].neighbors.Length; ++x)
            {
                if (!openList[0].neighbors[x].locked && !closedList.Contains(openList[0].neighbors[x]))
                {
                    if (openList.Contains(openList[0].neighbors[x]))
                    {
                        float tmpInt = openList[0].gScore + openList[0].neighbors[x].baseGScore;

                        if (calcShortest && tmpInt < openList[0].neighbors[x].gScore)
                        {
                            int y = openList.IndexOf(openList[0].neighbors[x]);
                            openList[y].prevNode = openList[0];
                            openList[y].SetScores(targetNode);
                        }
                        else if (!calcShortest && tmpInt > openList[0].neighbors[x].gScore)
                        {
                            int y = openList.IndexOf(openList[0].neighbors[x]);
                            openList[y].prevNode = openList[0];
                            openList[y].SetScores(targetNode);
                        }

                        continue;
                    }

                    openList[0].neighbors[x].prevNode = openList[0];
                    openList[0].neighbors[x].SetScores(targetNode);
                    openList.Add(openList[0].neighbors[x]);
                }
            }

            closedList.Add(openList[0]);
            openList.RemoveAt(0);

            if (useAStar)
            {
                if (calcShortest)
                    openList.Sort((node1, node2) => node1.fScore.CompareTo(node2.fScore));
                else
                    openList.Sort((node1, node2) => node2.fScore.CompareTo(node1.fScore));
            }
            else
            {
                if (calcShortest)
                    openList.Sort((node1, node2) => node1.gScore.CompareTo(node2.gScore));
                else
                    openList.Sort((node1, node2) => node2.gScore.CompareTo(node1.gScore));
            }
        }

        if(targetNode.prevNode == null)
        {
            curState = State.STOP;
            return null;
        }

        List<DijkNode> tmpPath = new List<DijkNode>();
        Queue<Node> finPath = new Queue<Node>();

        tmpPath.Add(targetNode);

        while(tmpPath[0].prevNode != null)
        {
            tmpPath.Insert(0, tmpPath[0].prevNode);
        }
        for(int x = 0; x < tmpPath.Count; ++x)
        {
            finPath.Enqueue(tmpPath[x]);
        }

        return finPath;
    }

    bool DifficultyCheck()
    {
        return difficulty == AIDifficulty.Expert || (difficulty == AIDifficulty.Intermediate && Random.value >= 0.5f);
    }

    /// <summary>
    /// Determines if the player is standing on something
    /// </summary>
    /// <returns>returns true if the player is standing on something</returns>
    bool IsGrounded()
    {
        return Physics.Raycast(transform.position, Vector3.down, agent.height / 1.9f);
    }
}
